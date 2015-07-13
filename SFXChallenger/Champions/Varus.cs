﻿#region License

/*
 Copyright 2014 - 2015 Nikita Bernthaler
 Varus.cs is part of SFXChallenger.

 SFXChallenger is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 SFXChallenger is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with SFXChallenger. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion License

#region

using System;
using System.Collections.Generic;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SFXChallenger.Abstracts;
using SFXChallenger.Enumerations;
using SFXChallenger.Helpers;
using SFXChallenger.Managers;
using SFXLibrary;
using SFXLibrary.Logger;
using Orbwalking = SFXChallenger.Wrappers.Orbwalking;
using TargetSelector = SFXChallenger.Wrappers.TargetSelector;

#endregion

namespace SFXChallenger.Champions
{
    internal class Varus : Champion
    {
        protected override ItemFlags ItemFlags
        {
            get { return ItemFlags.Offensive | ItemFlags.Defensive | ItemFlags.Flee; }
        }

        protected override void OnLoad()
        {
            Core.OnPostUpdate += OnCorePostUpdate;
            Orbwalking.AfterAttack += OnOrbwalkingAfterAttack;
            AntiGapcloser.OnEnemyGapcloser += OnEnemyGapcloser;
            Interrupter2.OnInterruptableTarget += OnInterruptableTarget;
        }

        protected override void OnUnload()
        {
            Core.OnPostUpdate -= OnCorePostUpdate;
            Orbwalking.AfterAttack -= OnOrbwalkingAfterAttack;
            AntiGapcloser.OnEnemyGapcloser -= OnEnemyGapcloser;
            Interrupter2.OnInterruptableTarget -= OnInterruptableTarget;
        }

        protected override void AddToMenu()
        {
            var comboMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("G_Combo"), Menu.Name + ".combo"));
            HitchanceManager.AddToMenu(
                comboMenu.AddSubMenu(new Menu(Global.Lang.Get("F_MH"), comboMenu.Name + ".hitchance")), "combo",
                new Dictionary<string, int> { { "E", 1 }, { "R", 2 } });
            comboMenu.AddItem(
                new MenuItem(comboMenu.Name + ".q-always", "Q " + Global.Lang.Get("G_Always")).SetValue(false));
            comboMenu.AddItem(new MenuItem(comboMenu.Name + ".q-stacks", "Q " + Global.Lang.Get("G_StacksIsOrMore")))
                .SetValue(new Slider(3, 1, 3));
            comboMenu.AddItem(new MenuItem(comboMenu.Name + ".q", Global.Lang.Get("G_UseQ")).SetValue(true));
            comboMenu.AddItem(new MenuItem(comboMenu.Name + ".e", Global.Lang.Get("G_UseE")).SetValue(true));
            comboMenu.AddItem(new MenuItem(comboMenu.Name + ".r", Global.Lang.Get("G_UseR")).SetValue(true));

            var harassMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("G_Harass"), Menu.Name + ".harass"));
            HitchanceManager.AddToMenu(
                harassMenu.AddSubMenu(new Menu(Global.Lang.Get("F_MH"), harassMenu.Name + ".hitchance")), "harass",
                new Dictionary<string, int> { { "E", 2 } });
            ManaManager.AddToMenu(harassMenu, "harass", ManaCheckType.Minimum, ManaValueType.Percent);
            harassMenu.AddItem(
                new MenuItem(harassMenu.Name + ".q-always", "Q " + Global.Lang.Get("G_Always")).SetValue(false));
            harassMenu.AddItem(new MenuItem(harassMenu.Name + ".q-stacks", "Q " + Global.Lang.Get("G_StacksIsOrMore")))
                .SetValue(new Slider(3, 1, 3));
            harassMenu.AddItem(new MenuItem(harassMenu.Name + ".q", Global.Lang.Get("G_UseQ")).SetValue(true));
            harassMenu.AddItem(new MenuItem(harassMenu.Name + ".e", Global.Lang.Get("G_UseE")).SetValue(true));

            var laneclearMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("G_LaneClear"), Menu.Name + ".lane-clear"));
            ManaManager.AddToMenu(laneclearMenu, "lane-clear", ManaCheckType.Minimum, ManaValueType.Percent);
            laneclearMenu.AddItem(new MenuItem(laneclearMenu.Name + ".q", Global.Lang.Get("G_UseQ")).SetValue(true));
            laneclearMenu.AddItem(new MenuItem(laneclearMenu.Name + ".e", Global.Lang.Get("G_UseE")).SetValue(true));
            laneclearMenu.AddItem(
                new MenuItem(laneclearMenu.Name + ".min", Global.Lang.Get("G_Min")).SetValue(new Slider(3, 1, 5)));

            var ultimateMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("G_Ultimate"), Menu.Name + ".ultimate"));

            var uComboMenu = ultimateMenu.AddSubMenu(new Menu(Global.Lang.Get("G_Combo"), ultimateMenu.Name + ".combo"));
            uComboMenu.AddItem(
                new MenuItem(uComboMenu.Name + ".min", "R " + Global.Lang.Get("G_Min")).SetValue(new Slider(2, 1, 5)));
            uComboMenu.AddItem(new MenuItem(uComboMenu.Name + ".enabled", Global.Lang.Get("G_Enabled")).SetValue(true));

            var uAutoMenu = ultimateMenu.AddSubMenu(new Menu(Global.Lang.Get("G_Auto"), ultimateMenu.Name + ".auto"));

            var autoGapMenu =
                uAutoMenu.AddSubMenu(new Menu(Global.Lang.Get("G_Gapcloser"), uAutoMenu.Name + ".gapcloser"));
            foreach (var enemy in
                GameObjects.EnemyHeroes.Where(
                    e =>
                        AntiGapcloser.Spells.Any(
                            s => s.ChampionName.Equals(e.ChampionName, StringComparison.OrdinalIgnoreCase))))
            {
                autoGapMenu.AddItem(
                    new MenuItem(autoGapMenu.Name + "." + enemy.ChampionName, enemy.ChampionName).SetValue(true));
            }

            var autoInterruptMenu =
                uAutoMenu.AddSubMenu(new Menu(Global.Lang.Get("G_InterruptSpell"), uAutoMenu.Name + ".interrupt"));
            foreach (var enemy in GameObjects.EnemyHeroes)
            {
                autoInterruptMenu.AddItem(
                    new MenuItem(autoInterruptMenu.Name + "." + enemy.ChampionName, enemy.ChampionName).SetValue(false));
            }

            uAutoMenu.AddItem(
                new MenuItem(uAutoMenu.Name + ".min", "R " + Global.Lang.Get("G_Min")).SetValue(new Slider(3, 1, 5)));
            uAutoMenu.AddItem(new MenuItem(uAutoMenu.Name + ".enabled", Global.Lang.Get("G_Enabled")).SetValue(true));

            var uAssistedMenu =
                ultimateMenu.AddSubMenu(new Menu(Global.Lang.Get("G_Assisted"), ultimateMenu.Name + ".assisted"));
            uAssistedMenu.AddItem(
                new MenuItem(uAssistedMenu.Name + ".min", "R " + Global.Lang.Get("G_Min")).SetValue(new Slider(2, 1, 5)));
            uAssistedMenu.AddItem(
                new MenuItem(uAssistedMenu.Name + ".hotkey", Global.Lang.Get("G_Hotkey")).SetValue(
                    new KeyBind('R', KeyBindType.Press)));
            uAssistedMenu.AddItem(
                new MenuItem(uAssistedMenu.Name + ".move-cursor", Global.Lang.Get("G_MoveCursor")).SetValue(true));
            uAssistedMenu.AddItem(
                new MenuItem(uAssistedMenu.Name + ".enabled", Global.Lang.Get("G_Enabled")).SetValue(true));

            var killstealMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("G_Killsteal"), Menu.Name + ".killsteal"));
            killstealMenu.AddItem(new MenuItem(killstealMenu.Name + ".q", Global.Lang.Get("G_UseQ")).SetValue(true));

            var fleeMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("G_Flee"), Menu.Name + ".flee"));
            fleeMenu.AddItem(new MenuItem(fleeMenu.Name + ".e", Global.Lang.Get("G_UseE")).SetValue(true));

            var miscMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("G_Miscellaneous"), Menu.Name + ".miscellaneous"));
            miscMenu.AddItem(
                new MenuItem(miscMenu.Name + ".e-gapcloser", "E " + Global.Lang.Get("G_Gapcloser")).SetValue(false));
        }

        protected override void SetupSpells()
        {
            Q = new Spell(SpellSlot.Q, 925f);
            Q.SetSkillshot(0.25f, 70f, 1650f, false, SkillshotType.SkillshotLine);
            Q.SetCharged("VarusQ", "VarusQ", 250, 1600, 1.2f);

            W = new Spell(SpellSlot.W, 0f);

            E = new Spell(SpellSlot.E, 925f);
            E.SetSkillshot(0.50f, 250f, 1400f, false, SkillshotType.SkillshotCircle);

            R = new Spell(SpellSlot.R, 1075f);
            R.SetSkillshot(0.25f, 120f, 1950f, true, SkillshotType.SkillshotLine);
        }

        private void OnCorePostUpdate(EventArgs args)
        {
            try
            {
                if (Menu.Item(Menu.Name + ".ultimate.assisted.enabled").GetValue<bool>() &&
                    Menu.Item(Menu.Name + ".ultimate.assisted.hotkey").GetValue<KeyBind>().Active && R.IsReady())
                {
                    if (Menu.Item(Menu.Name + ".ultimate.assisted.move-cursor").GetValue<bool>())
                    {
                        Orbwalking.MoveTo(Game.CursorPos, Orbwalker.HoldAreaRadius);
                    }

                    RLogic(
                        TargetSelector.GetTarget(R.Range, LeagueSharp.Common.TargetSelector.DamageType.Physical),
                        R.GetHitChance("combo"),
                        Menu.Item(Menu.Name + ".ultimate.assisted.min").GetValue<Slider>().Value);
                }

                if (Menu.Item(Menu.Name + ".ultimate.auto.enabled").GetValue<bool>() && R.IsReady())
                {
                    RLogic(
                        TargetSelector.GetTarget(R.Range, LeagueSharp.Common.TargetSelector.DamageType.Physical),
                        R.GetHitChance("combo"), Menu.Item(Menu.Name + ".ultimate.auto.min").GetValue<Slider>().Value);
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnEnemyGapcloser(ActiveGapcloser args)
        {
            try
            {
                if (!args.Sender.IsEnemy)
                {
                    return;
                }

                var endPos = args.End;
                if (args.Sender.ChampionName.Equals("Fizz", StringComparison.OrdinalIgnoreCase))
                {
                    endPos = args.Start.Extend(endPos, 550);
                }

                if (Menu.Item(Menu.Name + ".miscellaneous.e-gapcloser").GetValue<bool>() &&
                    endPos.Distance(Player.Position) < E.Range)
                {
                    E.Cast(endPos);
                }
                if (Menu.Item(Menu.Name + ".ultimate.auto.enabled").GetValue<bool>() &&
                    Menu.Item(Menu.Name + ".ultimate.auto.gapcloser." + args.Sender.ChampionName).GetValue<bool>())
                {
                    RLogic(args.Sender, HitChance.High, 1);
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnInterruptableTarget(Obj_AI_Hero sender, Interrupter2.InterruptableTargetEventArgs args)
        {
            try
            {
                if (sender.IsEnemy && args.DangerLevel >= Interrupter2.DangerLevel.High &&
                    Menu.Item(Menu.Name + ".ultimate.auto.enabled").GetValue<bool>() &&
                    Menu.Item(Menu.Name + ".ultimate.auto.interrupt." + sender.ChampionName).GetValue<bool>())
                {
                    RLogic(sender, HitChance.High, 1);
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnOrbwalkingAfterAttack(AttackableUnit unit, AttackableUnit target)
        {
            if (unit.IsMe)
            {
                if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo)
                {
                    var enemy = target as Obj_AI_Hero;
                    if (enemy != null)
                    {
                        ItemManager.Muramana(true);
                        ItemManager.UseComboItems(enemy);
                        SummonerManager.UseComboSummoners(enemy);
                    }
                }
                else
                {
                    ItemManager.Muramana(false);
                }
            }
        }

        protected override void Combo()
        {
            if (Menu.Item(Menu.Name + ".combo.q").GetValue<bool>() && Q.IsReady())
            {
                var target = TargetSelector.GetTarget(
                    Q.ChargedMaxRange, LeagueSharp.Common.TargetSelector.DamageType.Physical);
                if (Menu.Item(Menu.Name + ".combo.q-always").GetValue<bool>() ||
                    Menu.Item(Menu.Name + ".combo.q-stacks").GetValue<Slider>().Value >= GetWStacks(target) ||
                    Q.IsCharging)
                {
                    QLogic(target);
                }
            }
            if (Menu.Item(Menu.Name + ".combo.e").GetValue<bool>() && E.IsReady())
            {
                ELogic(
                    TargetSelector.GetTarget(E.Range, LeagueSharp.Common.TargetSelector.DamageType.Physical),
                    E.GetHitChance("combo"));
            }
            if (Menu.Item(Menu.Name + ".ultimate.combo.enabled").GetValue<bool>() && R.IsReady())
            {
                RLogic(
                    TargetSelector.GetTarget(R.Range, LeagueSharp.Common.TargetSelector.DamageType.Physical),
                    R.GetHitChance("combo"), Menu.Item(Menu.Name + ".ultimate.combo.min").GetValue<Slider>().Value);
            }
        }

        protected override void Harass()
        {
            if (!ManaManager.Check("harass") && !Q.IsCharging)
            {
                return;
            }
            if (Menu.Item(Menu.Name + ".harass.q").GetValue<bool>() && Q.IsReady())
            {
                var target = TargetSelector.GetTarget(
                    Q.ChargedMaxRange, LeagueSharp.Common.TargetSelector.DamageType.Physical);
                if (Menu.Item(Menu.Name + ".harass.q-always").GetValue<bool>() ||
                    Menu.Item(Menu.Name + ".harass.q-stacks").GetValue<Slider>().Value >= GetWStacks(target) ||
                    Q.IsCharging)
                {
                    QLogic(target);
                }
            }
            if (Menu.Item(Menu.Name + ".harass.e").GetValue<bool>() && E.IsReady())
            {
                ELogic(
                    TargetSelector.GetTarget(E.Range, LeagueSharp.Common.TargetSelector.DamageType.Physical),
                    E.GetHitChance("harass"));
            }
        }

        private void QLogic(Obj_AI_Hero target)
        {
            if (!Q.IsCharging)
            {
                Q.StartCharging();
            }
            if (Q.IsCharging)
            {
                var pred = Q.GetPrediction(target);
                var distance =
                    Player.ServerPosition.Distance(
                        pred.UnitPosition + 200 * (pred.UnitPosition - Player.ServerPosition).Normalized(), true);
                if (distance < Q.RangeSqr)
                {
                    Q.Cast(pred.CastPosition);
                }
            }
        }

        private void ELogic(Obj_AI_Hero target, HitChance hitChance)
        {
            if (Q.IsCharging)
            {
                return;
            }
            var pred = E.GetPrediction(target);
            if (pred.Hitchance >= hitChance)
            {
                E.Cast(pred.CastPosition);
            }
        }

        private void RLogic(Obj_AI_Hero target, HitChance hitChance, int min)
        {
            var pred = R.GetPrediction(target);
            if (pred.Hitchance >= hitChance && (target.CountEnemiesInRange(450) - 1) >= min)
            {
                R.Cast(pred.CastPosition);
            }
        }

        protected override void LaneClear()
        {
            if (!ManaManager.Check("lane-clear") && !Q.IsCharging)
            {
                return;
            }

            var min = Menu.Item(Menu.Name + ".lane-clear.min").GetValue<Slider>().Value;

            if (Menu.Item(Menu.Name + ".lane-clear.q").GetValue<bool>() && Q.IsReady())
            {
                Casting.Farm(Q, min);
            }
            if (Menu.Item(Menu.Name + ".lane-clear.e").GetValue<bool>() && E.IsReady())
            {
                Casting.Farm(E, min);
            }
        }

        protected override void Flee()
        {
            if (Menu.Item(Menu.Name + ".flee.e").GetValue<bool>() && E.IsReady())
            {
                ELogic(
                    GameObjects.EnemyHeroes.Where(e => e.IsValidTarget(E.Range))
                        .OrderBy(e => e.Position.Distance(Player.Position))
                        .FirstOrDefault(), HitChance.High);
            }
        }

        protected override void Killsteal()
        {
            if (Menu.Item(Menu.Name + ".killsteal.q").GetValue<bool>() && Q.IsReady())
            {
                var killable = GameObjects.EnemyHeroes.FirstOrDefault(e => Q.IsKillable(e));
                if (killable != null)
                {
                    QLogic(killable);
                }
            }
        }

        private int GetWStacks(Obj_AI_Base target)
        {
            return
                target.Buffs.Where(
                    b =>
                        b.Name.Equals("varuswdebuff", StringComparison.OrdinalIgnoreCase) &&
                        target.IsValidTarget(Q.Range)).Select(b => b.Count).FirstOrDefault();
        }
    }
}