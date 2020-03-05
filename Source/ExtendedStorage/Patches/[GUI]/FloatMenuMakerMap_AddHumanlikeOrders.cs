﻿using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ExtendedStorage.Patches
{
    [HarmonyPatch(typeof(FloatMenuMakerMap), "AddHumanlikeOrders")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal class FloatMenuMakerMap_AddHumanlikeOrders
    {
        /// <remarks>
        ///     Special case ClothingRack - add in 'Equip XYZ' options for all stored elements, not just the first
        /// </remarks>
        public static void Postfix(Vector3 clickPos, Pawn pawn, List<FloatMenuOption> opts)
        {
            IntVec3 c = IntVec3.FromVector3(clickPos);

            Building_ExtendedStorage storage = pawn.Map.thingGrid.ThingAt<Building_ExtendedStorage>(c);
            if (storage?.def.defName == @"Storage_Locker")
            {
                List<Apparel> apparels = pawn.Map.thingGrid.ThingsAt(c).OfType<Apparel>().ToList();

                if (apparels.Count > 1)
                {
                    FloatMenuOption baseOption = CreateMenuOptionApparel(pawn, apparels[0]);
                    int baseIndex = opts.FirstIndexOf(mo => mo.Label == baseOption.Label); // maybe this is hinky.... can this ever get the wrong option if comparing just by label???

                    IEnumerable<FloatMenuOption> extraOptions = apparels.Skip(1).Select(a => CreateMenuOptionApparel(pawn, a));

                    if (baseIndex == -1)
                        opts.AddRange(extraOptions);
                    else
                        opts.InsertRange(baseIndex + 1, extraOptions);
                }
            }
            else if (storage?.def.defName == @"Storage_LargeWeaponsRack")
            {
                List<ThingWithComps> equipment = pawn.Map.thingGrid.ThingsAt(c).OfType<ThingWithComps>().Where(t => t.TryGetComp<CompEquippable>() != null).ToList();

                if (equipment.Count > 1)
                {
                    FloatMenuOption baseOption = CreateMenuOptionEquipment(pawn, equipment[0]);
                    int baseIndex = opts.FirstIndexOf(mo => mo.Label == baseOption.Label); // maybe this is hinky.... can this ever get the wrong option if comparing just by label???

                    IEnumerable<FloatMenuOption> extraOptions = equipment.Skip(1).Select(eq => CreateMenuOptionEquipment(pawn, eq));

                    if (baseIndex == -1)
                        opts.AddRange(extraOptions);
                    else
                        opts.InsertRange(baseIndex + 1, extraOptions);
                }
            }
        }

        private static FloatMenuOption CreateMenuOptionEquipment(Pawn pawn, ThingWithComps equipment)
        {
            string labelShort = equipment.LabelShort;
            FloatMenuOption option;
            if (equipment.def.IsWeapon && pawn.story.WorkTagIsDisabled(WorkTags.Violent))
            {
                option = new FloatMenuOption("CannotEquip".Translate(labelShort) + " (" + "IsIncapableOfViolenceLower".Translate(pawn.LabelShort, pawn) + ")", null, MenuOptionPriority.Default, null, null, 0f, null, null);
            }
            else if (!pawn.CanReach(equipment, PathEndMode.ClosestTouch, Danger.Deadly, false, TraverseMode.ByPawn))
            {
                option = new FloatMenuOption("CannotEquip".Translate(labelShort) + " (" + "NoPath".Translate() + ")", null, MenuOptionPriority.Default, null, null, 0f, null, null);
            }
            else if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
            {
                option = new FloatMenuOption("CannotEquip".Translate(labelShort) + " (" + "Incapable".Translate() + ")", null, MenuOptionPriority.Default, null, null, 0f, null, null);
            }
            else if (equipment.IsBurning())
            {
                option = new FloatMenuOption("CannotEquip".Translate(labelShort) + " (" + "BurningLower".Translate() + ")", null, MenuOptionPriority.Default, null, null, 0f, null, null);
            }
            else
            {
                string textUpdated = "Equip".Translate(labelShort);
                if (equipment.def.IsRangedWeapon && pawn.story != null && pawn.story.traits.HasTrait(TraitDefOf.Brawler))
                {
                    textUpdated = textUpdated + " " + "EquipWarningBrawler".Translate();
                }
                option = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(textUpdated, delegate
                {
                    equipment.SetForbidden(false, true);
                    pawn.jobs.TryTakeOrderedJob(new Job(JobDefOf.Equip, equipment), JobTag.Misc);
                    MoteMaker.MakeStaticMote(equipment.DrawPos, equipment.Map, ThingDefOf.Mote_FeedbackEquip, 1f);
                    PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.EquippingWeapons, KnowledgeAmount.Total);
                }, MenuOptionPriority.High, null, null, 0f, null, null), pawn, equipment, "ReservedBy");
            }
            return option;
        }

        private static FloatMenuOption CreateMenuOptionApparel(Pawn pawn, Apparel apparel)
        {
            // original code taken from FloatMenuMakerMap_AddHumanlikeOrders
            if (!pawn.CanReach(apparel, PathEndMode.ClosestTouch, Danger.Deadly, false, TraverseMode.ByPawn))
            {
                return new FloatMenuOption("CannotWear".Translate(apparel.Label, apparel) + " (" + "NoPath".Translate() + ")", null, MenuOptionPriority.Default, null, null, 0f, null, null);
            }
            else if (apparel.IsBurning())
            {
                return new FloatMenuOption("CannotWear".Translate(apparel.Label, apparel) + " (" + "BurningLower".Translate() + ")", null, MenuOptionPriority.Default, null, null, 0f, null, null);
            }
            else if (!ApparelUtility.HasPartsToWear(pawn, apparel.def))
            {
                return new FloatMenuOption("CannotWear".Translate(apparel.Label, apparel) + " (" + "CannotWearBecauseOfMissingBodyParts".Translate() + ")", null, MenuOptionPriority.Default, null, null, 0f, null, null);
            }
            else
            {
                return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("ForceWear".Translate(apparel.LabelShort, apparel), delegate
                                                                                                                                         {
                                                                                                                                             apparel.SetForbidden(false, true);
                                                                                                                                             Job job = new Job(JobDefOf.Wear, apparel);
                                                                                                                                             pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                                                                                                                                         }, MenuOptionPriority.High, null, null, 0f, null, null), pawn, apparel, "ReservedBy");
            }
        }
    }
}
