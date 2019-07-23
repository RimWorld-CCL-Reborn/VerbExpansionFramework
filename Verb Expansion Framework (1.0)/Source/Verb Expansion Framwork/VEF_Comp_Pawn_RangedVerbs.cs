﻿using System;
using System.Collections.Generic;
using Verse;
using RimWorld;
using UnityEngine;

namespace VerbExpansionFramework
{
    public class VEF_Comp_Pawn_RangedVerbs : ThingComp
    {

        public Pawn Pawn
        {
            get
            {
                return this.pawn ?? (this.pawn = (Pawn) this.parent);
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            if (respawningAfterLoad == false)
            {
                TryGetRangedVerb(null);
            }
            else
            {
                UpdateRangedVerbs();
            }
        }

        public override void CompTick()
        {
            if (this.Pawn.Spawned && this.Pawn.IsHashIntervalTick(60))
            {
                TryGetRangedVerb(curRangedVerbTarget);
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            this.rangedVerbGizmo = new VEF_Gizmo_SwitchRangedVerb(this.Pawn)
            {
                disabled = true,
                disabledReason = "IsNotDrafted".Translate(this.Pawn.LabelShortCap, this.Pawn),
                order = +1f
            };
            if (this.Pawn.IsColonist)
            {
                if (this.Pawn.Drafted)
                {
                    this.rangedVerbGizmo.disabled = false;
                }
                if (curRangedVerb == null || (rangedVerbs.Count == 1 && curRangedVerb.EquipmentCompSource != null && curRangedVerb.verbProps.isPrimary))
                {
                    this.visible = false;
                }
                else
                {
                    this.visible = true;
                }
            }
            else
            {
                this.visible = false;
            }
            yield return this.rangedVerbGizmo;
        }

        public Verb TryGetRangedVerb(Thing target)
        {
            UpdateRangedVerbs();
            if (this.curRangedVerb == null || !this.curRangedVerb.IsStillUsableBy(this.Pawn))
            {
                ChooseRangedVerb(target);
            }
            else if (!this.Pawn.IsColonist || this.Pawn.InMentalState)
            {
                if (this.curRangedVerbTarget != target || Find.TickManager.TicksGame >= this.curRangedVerbUpdateTick + 60 || !this.curRangedVerb.IsStillUsableBy(this.Pawn) || !this.curRangedVerb.IsUsableOn(target))
                {
                    ChooseRangedVerb(target);
                }
            }
            return this.curRangedVerb;
        }

        private void ChooseRangedVerb(Thing target)
        {
            List<VerbEntry> updatedAvailableVerbsList = getRangedVerbs;
            if (updatedAvailableVerbsList.NullOrEmpty())
            {
                SetCurRangedVerb(null, null);
            }
            else if (updatedAvailableVerbsList.Count == 1)
            {
                SetCurRangedVerb(updatedAvailableVerbsList[0].verb, target);
            }
            else
            {
                float highestScore = 0f;
                int highestScoreIndex = -1;

                if (target == null)
                {
                    for (int i = 0; i < updatedAvailableVerbsList.Count; i++)
                    {
                        VerbEntry verbEntry = updatedAvailableVerbsList[i];
                        Verb v = verbEntry.verb;
                        ThingDef verbProjectile = v.GetProjectile();
                        int projectileDamageAmount = (v.EquipmentCompSource == null) ? verbProjectile.projectile.GetDamageAmount(1f) : verbProjectile.projectile.GetDamageAmount(v.EquipmentCompSource.parent);
                        List<float> accuracyList = (v.EquipmentCompSource == null) ? new List<float>() { v.verbProps.accuracyLong, v.verbProps.accuracyMedium, v.verbProps.accuracyShort, v.verbProps.accuracyTouch } : new List<float>() { v.EquipmentCompSource.parent.GetStatValue(StatDefOf.AccuracyLong), v.EquipmentCompSource.parent.GetStatValue(StatDefOf.AccuracyMedium), v.EquipmentCompSource.parent.GetStatValue(StatDefOf.AccuracyShort), v.EquipmentCompSource.parent.GetStatValue(StatDefOf.AccuracyTouch) };
                        accuracyList.Sort();
                        accuracyList.Reverse();
                        float accuracyValue = (accuracyList[0] + accuracyList[1]) / 2;
                        int burstShotCount = (v.verbProps.burstShotCount == 0) ? 1 : v.verbProps.burstShotCount;
                        float fullCycleTime = v.verbProps.AdjustedFullCycleTime(v, this.Pawn);

                        float currentScore = ((accuracyValue * projectileDamageAmount * burstShotCount) / (fullCycleTime)) + v.GetProjectile().projectile.explosionRadius - (v.verbProps.forcedMissRadius / burstShotCount);

                        if (currentScore > highestScore)
                        {
                            highestScore = currentScore;
                            highestScoreIndex = i;
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < updatedAvailableVerbsList.Count; i++)
                    {
                        VerbEntry verbEntry = updatedAvailableVerbsList[i];
                        Verb v = verbEntry.verb;
                        ThingDef verbProjectile = v.GetProjectile();
                        int projectileDamageAmount = (v.EquipmentCompSource == null) ? verbProjectile.projectile.GetDamageAmount(1f) : verbProjectile.projectile.GetDamageAmount(v.EquipmentCompSource.parent);
                        float accuracyValue = Verse.ShotReport.HitReportFor(Pawn, v, target).TotalEstimatedHitChance;
                        int burstShotCount = (v.verbProps.burstShotCount == 0) ? 1 : v.verbProps.burstShotCount;
                        float fullCycleTime = v.verbProps.AdjustedFullCycleTime(v, this.Pawn);

                        float currentScore = ((accuracyValue * projectileDamageAmount * burstShotCount) / (fullCycleTime)) + v.GetProjectile().projectile.explosionRadius - (v.verbProps.forcedMissRadius / burstShotCount);
                        if (target != null && v.IsIncendiary() && target.CanEverAttachFire() && !target.IsBurning())
                        {
                            currentScore += 10;
                        }
                        Pawn targetPawn = target as Pawn;
                        if (targetPawn != null && v.IsEMP() && !targetPawn.RaceProps.IsFlesh)
                        {
                            currentScore += 10;
                        }

                        if (currentScore > highestScore)
                        {
                            highestScore = currentScore;
                            highestScoreIndex = i;
                        }
                    }
                }

                if (highestScoreIndex != -1)
                {
                    SetCurRangedVerb(updatedAvailableVerbsList[highestScoreIndex].verb, target);
                }
            }
        }

        public void UpdateRangedVerbs()
        {
            this.rangedVerbs.Clear();
            List<Verb> allVerbs = this.Pawn.verbTracker.AllVerbs;
            if (!allVerbs.NullOrEmpty())
            {
                for (int i = 0; i < allVerbs.Count; i++)
                {
                    if (!allVerbs[i].IsMeleeAttack && allVerbs[i].IsStillUsableBy(this.Pawn))
                    {
                        this.rangedVerbs.Add(new VerbEntry(allVerbs[i], this.Pawn));
                    }
                }
            }
            if (this.Pawn.equipment != null)
            {
                List<ThingWithComps> allEquipmentListForReading = this.Pawn.equipment.AllEquipmentListForReading;
                for (int j = 0; j < allEquipmentListForReading.Count; j++)
                {
                    ThingWithComps thingWithComps = allEquipmentListForReading[j];
                    CompEquippable equipmentComp = thingWithComps.GetComp<CompEquippable>();
                    if (equipmentComp != null)
                    {
                        List<Verb> allEquipmentVerbs = equipmentComp.AllVerbs;
                        if (allEquipmentVerbs != null)
                        {
                            for (int k = 0; k < allEquipmentVerbs.Count; k++)
                            {
                                if (!allEquipmentVerbs[k].IsMeleeAttack && allEquipmentVerbs[k].IsStillUsableBy(this.Pawn))
                                {
                                    this.rangedVerbs.Add(new VerbEntry(allEquipmentVerbs[k], this.Pawn));
                                }
                            }
                        }
                    }
                }
            }
            if (this.Pawn.apparel != null)
            {
                List<Apparel> wornApparel = this.Pawn.apparel.WornApparel;
                for (int l = 0; l < wornApparel.Count; l++)
                {
                    Apparel apparel = wornApparel[l];
                    CompEquippable apparelComp = apparel.GetComp<CompEquippable>();
                    if (apparelComp != null)
                    {
                        List<Verb> allApparelVerbs = apparelComp.AllVerbs;
                        if (!allApparelVerbs.NullOrEmpty())
                        {
                            for (int m = 0; m < allApparelVerbs.Count; m++)
                            {
                                if (!allApparelVerbs[m].IsMeleeAttack && allApparelVerbs[m].IsStillUsableBy(this.Pawn))
                                {
                                    this.rangedVerbs.Add(new VerbEntry(allApparelVerbs[m], this.Pawn));
                                }
                            }
                        }
                    }
                }
            }
            foreach (Verb verb in this.Pawn.health.hediffSet.GetHediffsVerbs())
            {
                if (!verb.IsMeleeAttack && verb.IsStillUsableBy(this.Pawn))
                {
                    this.rangedVerbs.Add(new VerbEntry(verb, this.Pawn));
                }
            }
            return;
        }

        public Verb CurRangedVerb
        {
            get
            {
                return curRangedVerb;
            }
        }

        public List<VerbEntry> getRangedVerbs
        {
            get
            {
                UpdateRangedVerbs();
                return this.rangedVerbs;
            }
        }

        public void SetCurRangedVerb(Verb v, Thing target)
        {
            this.curRangedVerb = v;
            this.curRangedVerbTarget = target;
            if (Current.ProgramState != ProgramState.Playing)
            {
                this.curRangedVerbUpdateTick = 0;
            }
            else
            {
                this.curRangedVerbUpdateTick = Find.TickManager.TicksGame;
            }
        }

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving && this.curRangedVerb != null && !this.curRangedVerb.IsStillUsableBy(this.Pawn))
            {
                this.curRangedVerb = null;
            }
            Scribe_References.Look<Verb>(ref this.curRangedVerb, "curRangedVerb", false);
            Scribe_Values.Look<int>(ref this.curRangedVerbUpdateTick, "curRangedVerbUpdateTick", 0, false);
            Scribe_Values.Look<int>(ref this.Pawn.meleeVerbs.lastTerrainBasedVerbUseTick, "lastTerrainBasedVerbUseTick", -99999, false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && this.curRangedVerb != null && this.curRangedVerb.BuggedAfterLoading)
            {
                this.curRangedVerb = null;
                Log.Warning(this.Pawn.ToStringSafe<Pawn>() + " had a bugged ranged verb after loading.", false);
            }
        }

        private Pawn pawn;

        private Verb curRangedVerb;

        private Thing curRangedVerbTarget;

        private Gizmo rangedVerbGizmo;

        public bool visible = false;

        private int curRangedVerbUpdateTick;

        private List<VerbEntry> rangedVerbs = new List<VerbEntry>();

        private const int BestRangedVerbUpdateInterval = 60;
    }
}