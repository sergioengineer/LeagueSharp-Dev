#region License

/*
 Copyright 2014 - 2015 Nikita Bernthaler
 Kalista.cs is part of SFXChallenger.

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
using SFXChallenger.Args;
using SFXChallenger.Enumerations;
using SFXChallenger.Helpers;
using SFXChallenger.Library;
using SFXChallenger.Library.Logger;
using SFXChallenger.Managers;
using SFXChallenger.SFXTargetSelector;
using SharpDX;
using Collision = LeagueSharp.Common.Collision;
using DamageType = SFXChallenger.Enumerations.DamageType;
using ItemData = LeagueSharp.Common.Data.ItemData;
using MinionManager = SFXChallenger.Library.MinionManager;
using MinionOrderTypes = SFXChallenger.Library.MinionOrderTypes;
using MinionTeam = SFXChallenger.Library.MinionTeam;
using MinionTypes = SFXChallenger.Library.MinionTypes;
using Orbwalking = SFXChallenger.Wrappers.Orbwalking;
using Spell = SFXChallenger.Wrappers.Spell;
using TargetSelector = SFXChallenger.SFXTargetSelector.TargetSelector;
using Utils = SFXChallenger.Helpers.Utils;

#endregion

namespace SFXChallenger.Champions
{
    internal class Kalista : Champion
    {
        private Obj_AI_Hero _soulbound;

        protected override ItemFlags ItemFlags
        {
            get { return ItemFlags.Offensive | ItemFlags.Defensive | ItemFlags.Flee; }
        }

        protected override ItemUsageType ItemUsage
        {
            get { return ItemUsageType.AfterAttack; }
        }

        protected override void OnLoad()
        {
            Obj_AI_Base.OnProcessSpellCast += OnObjAiBaseProcessSpellCast;
            Spellbook.OnCastSpell += OnSpellbookCastSpell;
            Orbwalking.OnNonKillableMinion += OnOrbwalkingNonKillableMinion;

            CheckSoulbound();
        }

        protected override void SetupSpells()
        {
            Q = new Spell(SpellSlot.Q, 1200f);
            Q.SetSkillshot(0.25f, 40f, 1650f, true, SkillshotType.SkillshotLine);

            W = new Spell(SpellSlot.W, 5000f);

            E = new Spell(SpellSlot.E, 1000f);

            R = new Spell(SpellSlot.R, 1200f);
        }

        protected override void AddToMenu()
        {
            var ultimateMenu = Menu.AddSubMenu(new Menu("Ultimate", Menu.Name + ".ultimate"));

            var blitzMenu = ultimateMenu.AddSubMenu(new Menu("Blitzcrank", ultimateMenu.Name + ".blitzcrank"));
            HeroListManager.AddToMenu(
                blitzMenu.AddSubMenu(new Menu("Blacklist", blitzMenu.Name + ".blacklist")),
                new HeroListManagerArgs("blitzcrank")
                {
                    IsWhitelist = false,
                    Allies = false,
                    Enemies = true,
                    DefaultValue = false,
                    EnabledButton = false
                });
            blitzMenu.AddItem(new MenuItem(blitzMenu.Name + ".r", "Enabled").SetValue(true));

            var tahmMenu = ultimateMenu.AddSubMenu(new Menu("Tahm Kench", ultimateMenu.Name + ".tahm-kench"));
            HeroListManager.AddToMenu(
                tahmMenu.AddSubMenu(new Menu("Blacklist", tahmMenu.Name + ".blacklist")),
                new HeroListManagerArgs("tahm-kench")
                {
                    IsWhitelist = false,
                    Allies = false,
                    Enemies = true,
                    DefaultValue = false,
                    EnabledButton = false
                });
            tahmMenu.AddItem(new MenuItem(tahmMenu.Name + ".r", "Enabled").SetValue(true));

            ultimateMenu.AddItem(new MenuItem(ultimateMenu.Name + ".save", "Save Soulbound").SetValue(true));

            var comboMenu = Menu.AddSubMenu(new Menu("Combo", Menu.Name + ".combo"));
            HitchanceManager.AddToMenu(
                comboMenu.AddSubMenu(new Menu("Hitchance", comboMenu.Name + ".hitchance")), "combo",
                new Dictionary<string, HitChance> { { "Q", HitChance.VeryHigh } });
            ResourceManager.AddToMenu(
                comboMenu,
                new ResourceManagerArgs(
                    "combo-q", ResourceType.Mana, ResourceValueType.Percent, ResourceCheckType.Minimum)
                {
                    Prefix = "Q",
                    DefaultValue = 10
                });
            comboMenu.AddItem(new MenuItem(comboMenu.Name + ".q", "Use Q").SetValue(true));
            comboMenu.AddItem(new MenuItem(comboMenu.Name + ".e", "Use E").SetValue(true));
            comboMenu.AddItem(new MenuItem(comboMenu.Name + ".e-min", "E Fleeing Min.").SetValue(new Slider(8, 1, 20)));
            comboMenu.AddItem(new MenuItem(comboMenu.Name + ".minions", "Attack Minions").SetValue(false));

            var harassMenu = Menu.AddSubMenu(new Menu("Harass", Menu.Name + ".harass"));
            HitchanceManager.AddToMenu(
                harassMenu.AddSubMenu(new Menu("Hitchance", harassMenu.Name + ".hitchance")), "harass",
                new Dictionary<string, HitChance> { { "Q", HitChance.High } });
            ResourceManager.AddToMenu(
                harassMenu,
                new ResourceManagerArgs(
                    "harass-q", ResourceType.Mana, ResourceValueType.Percent, ResourceCheckType.Minimum)
                {
                    Prefix = "Q",
                    DefaultValue = 30
                });
            ResourceManager.AddToMenu(
                harassMenu,
                new ResourceManagerArgs(
                    "harass-e", ResourceType.Mana, ResourceValueType.Percent, ResourceCheckType.Minimum)
                {
                    Prefix = "E",
                    DefaultValue = 30
                });
            harassMenu.AddItem(new MenuItem(harassMenu.Name + ".q", "Use Q").SetValue(true));
            harassMenu.AddItem(new MenuItem(harassMenu.Name + ".e", "Use E").SetValue(true));
            harassMenu.AddItem(new MenuItem(harassMenu.Name + ".e-min", "E Min.").SetValue(new Slider(4, 1, 20)));

            var laneclearMenu = Menu.AddSubMenu(new Menu("Lane Clear", Menu.Name + ".lane-clear"));
            ResourceManager.AddToMenu(
                laneclearMenu,
                new ResourceManagerArgs(
                    "lane-clear", ResourceType.Mana, ResourceValueType.Percent, ResourceCheckType.Minimum)
                {
                    Advanced = true,
                    MaxValue = 101,
                    LevelRanges = new SortedList<int, int> { { 1, 6 }, { 6, 12 }, { 12, 18 } },
                    DefaultValues = new List<int> { 50, 30, 30 },
                    IgnoreJungleOption = true
                });
            laneclearMenu.AddItem(new MenuItem(laneclearMenu.Name + ".q", "Use Q").SetValue(true));
            laneclearMenu.AddItem(
                new MenuItem(laneclearMenu.Name + ".q-min", "Q Min. Hits").SetValue(new Slider(2, 1, 5)));
            laneclearMenu.AddItem(new MenuItem(laneclearMenu.Name + ".e", "Use E").SetValue(true));

            var lasthitMenu = Menu.AddSubMenu(new Menu("Last Hit", Menu.Name + ".lasthit"));
            ResourceManager.AddToMenu(
                lasthitMenu,
                new ResourceManagerArgs(
                    "lasthit", ResourceType.Mana, ResourceValueType.Percent, ResourceCheckType.Minimum)
                {
                    Advanced = true,
                    MaxValue = 101,
                    LevelRanges = new SortedList<int, int> { { 1, 6 }, { 6, 12 }, { 12, 18 } },
                    DefaultValues = new List<int> { 50, 30, 30 }
                });
            lasthitMenu.AddItem(new MenuItem(lasthitMenu.Name + ".e-siege", "E Siege Minion").SetValue(true));
            lasthitMenu.AddItem(new MenuItem(lasthitMenu.Name + ".e-unkillable", "E Unkillable").SetValue(true));
            lasthitMenu.AddItem(new MenuItem(lasthitMenu.Name + ".e-turret", "E Under Turret").SetValue(true));
            lasthitMenu.AddItem(new MenuItem(lasthitMenu.Name + ".separator", string.Empty));
            lasthitMenu.AddItem(new MenuItem(lasthitMenu.Name + ".e-jungle", "E Jungle").SetValue(true));
            lasthitMenu.AddItem(new MenuItem(lasthitMenu.Name + ".e-big", "E Dragon/Baron").SetValue(true));

            var killstealMenu = Menu.AddSubMenu(new Menu("Killsteal", Menu.Name + ".killsteal"));
            killstealMenu.AddItem(new MenuItem(killstealMenu.Name + ".e", "Use E").SetValue(true));

            var miscMenu = Menu.AddSubMenu(new Menu("Misc", Menu.Name + ".miscellaneous"));
            ResourceManager.AddToMenu(
                miscMenu,
                new ResourceManagerArgs("misc", ResourceType.Mana, ResourceValueType.Percent, ResourceCheckType.Minimum)
                {
                    Prefix = "E",
                    DefaultValue = 30
                });
            miscMenu.AddItem(new MenuItem(miscMenu.Name + ".e-reset", "E Harass Reset").SetValue(true));
            miscMenu.AddItem(
                new MenuItem(miscMenu.Name + ".w-baron", "Hotkey W Baron").SetValue(new KeyBind('J', KeyBindType.Press)));
            miscMenu.AddItem(
                new MenuItem(miscMenu.Name + ".w-dragon", "Hotkey W Dragon").SetValue(
                    new KeyBind('K', KeyBindType.Press)));

            IndicatorManager.AddToMenu(DrawingManager.Menu, true);
            IndicatorManager.Add(Q, true, false);
            IndicatorManager.Add(W, true, false);
            IndicatorManager.Add("E", Rend.GetDamage);
            IndicatorManager.Finale();

            Weights.GetItem("low-health").GetValueFunc = hero => hero.Health - Rend.GetDamage(hero);
            Weights.AddItem(
                new Weights.Item(
                    "w-stack", "W Stack", 10, false, hero => hero.HasBuff("kalistacoopstrikemarkally") ? 10 : 0));
        }

        private void OnSpellbookCastSpell(Spellbook sender, SpellbookCastSpellEventArgs args)
        {
            try
            {
                if (sender.Owner.IsMe && args.Slot == SpellSlot.Q && Player.IsDashing())
                {
                    args.Process = false;
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnObjAiBaseProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            try
            {
                if (sender.IsMe)
                {
                    if (args.SData.Name == "KalistaExpungeWrapper")
                    {
                        Orbwalking.ResetAutoAttackTimer();
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnOrbwalkingNonKillableMinion(AttackableUnit unit)
        {
            try
            {
                if (Menu.Item(Menu.Name + ".lasthit.e-unkillable").GetValue<bool>() && E.IsReady() &&
                    ResourceManager.Check("lasthit"))
                {
                    var target = unit as Obj_AI_Base;
                    if (target != null && Rend.IsKillable(target, true))
                    {
                        E.Cast();
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        protected override void OnPreUpdate()
        {
            if (E.IsReady())
            {
                var eBig = Menu.Item(Menu.Name + ".lasthit.e-big").GetValue<bool>();
                var eJungle = Menu.Item(Menu.Name + ".lasthit.e-jungle").GetValue<bool>();
                if (eBig || eJungle)
                {
                    if (eJungle && Player.Level >= 3 || eBig)
                    {
                        var creeps =
                            GameObjects.Jungle.Where(e => e.IsValidTarget(E.Range) && Rend.IsKillable(e, false))
                                .ToList();
                        if (eJungle && creeps.Any() ||
                            eBig &&
                            creeps.Any(
                                m =>
                                    (m.CharData.BaseSkinName.StartsWith("SRU_Dragon") ||
                                     m.CharData.BaseSkinName.StartsWith("SRU_Baron"))))
                        {
                            E.Cast();
                            return;
                        }
                    }
                }

                if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear ||
                    Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LastHit)
                {
                    var eSiege = Menu.Item(Menu.Name + ".lasthit.e-siege").GetValue<bool>();
                    var eTurret = Menu.Item(Menu.Name + ".lasthit.e-turret").GetValue<bool>();
                    var eReset = Menu.Item(Menu.Name + ".miscellaneous.e-reset").GetValue<bool>();

                    IEnumerable<Obj_AI_Minion> minions = new HashSet<Obj_AI_Minion>();
                    if (eSiege || eTurret || eReset)
                    {
                        minions =
                            GameObjects.EnemyMinions.Where(
                                e => e.IsValidTarget(E.Range) && Rend.IsKillable(e, e.HealthPercent < 25));
                    }

                    if (ResourceManager.Check("lasthit"))
                    {
                        if (eSiege)
                        {
                            if (
                                minions.Any(
                                    m =>
                                        (m.CharData.BaseSkinName.Contains("MinionSiege") ||
                                         m.CharData.BaseSkinName.Contains("Super"))))
                            {
                                E.Cast();
                                return;
                            }
                        }
                        if (eTurret)
                        {
                            if (minions.Any(m => Utils.UnderAllyTurret(m.Position)))
                            {
                                E.Cast();
                                return;
                            }
                        }
                    }

                    if (eReset && E.IsReady() && ResourceManager.Check("misc") &&
                        GameObjects.EnemyHeroes.Any(e => Rend.HasBuff(e) && e.IsValidTarget(E.Range)))
                    {
                        if (minions.Any())
                        {
                            E.Cast();
                            return;
                        }
                    }
                }
            }

            if (Menu.Item(Menu.Name + ".ultimate.save").GetValue<bool>() && _soulbound != null && R.IsReady() &&
                !_soulbound.InFountain())
            {
                var enemies = _soulbound.CountEnemiesInRange(500);
                var damage = IncomingDamageManager.GetDamage(_soulbound);
                if ((_soulbound.HealthPercent <= 10 && _soulbound.CountEnemiesInRange(500) > 0) ||
                    (_soulbound.HealthPercent <= 5 && damage > _soulbound.Health && enemies == 0) ||
                    (_soulbound.HealthPercent <= 50 && damage > _soulbound.Health && enemies > 0))
                {
                    R.Cast();
                }
            }

            if (Menu.Item(Menu.Name + ".miscellaneous.w-baron").GetValue<KeyBind>().Active && W.IsReady() &&
                !Player.IsWindingUp && Player.Distance(SummonersRift.River.Baron) <= W.Range)
            {
                W.Cast(SummonersRift.River.Baron);
            }
            if (Menu.Item(Menu.Name + ".miscellaneous.w-dragon").GetValue<KeyBind>().Active && W.IsReady() &&
                !Player.IsWindingUp && Player.Distance(SummonersRift.River.Dragon) <= W.Range)
            {
                W.Cast(SummonersRift.River.Dragon);
            }

            CheckSoulbound();

            if (_soulbound != null && _soulbound.Distance(Player) < R.Range && R.IsReady())
            {
                var blitz = Menu.Item(Menu.Name + ".ultimate.blitzcrank.r").GetValue<bool>();
                var tahm = Menu.Item(Menu.Name + ".ultimate.tahm-kench.r").GetValue<bool>();
                foreach (var enemy in
                    GameObjects.EnemyHeroes.Where(e => (blitz || tahm) && !e.IsDead && e.Distance(Player) < 3000))
                {
                    if (blitz)
                    {
                        var blitzBuff =
                            enemy.Buffs.FirstOrDefault(
                                b =>
                                    b.IsActive && b.Caster.NetworkId.Equals(_soulbound.NetworkId) &&
                                    b.Name.Equals("rocketgrab2", StringComparison.OrdinalIgnoreCase));
                        if (blitzBuff != null)
                        {
                            if (!HeroListManager.Check("blitzcrank", enemy))
                            {
                                if (!_soulbound.UnderTurret(false) && _soulbound.Distance(enemy) > 750f &&
                                    _soulbound.Distance(Player) > R.Range / 3f)
                                {
                                    R.Cast();
                                }
                            }
                            return;
                        }
                    }
                    if (tahm)
                    {
                        var tahmBuff =
                            enemy.Buffs.FirstOrDefault(
                                b =>
                                    b.IsActive && b.Caster.NetworkId.Equals(_soulbound.NetworkId) &&
                                    b.Name.Equals("tahmkenchwdevoured", StringComparison.OrdinalIgnoreCase));
                        if (tahmBuff != null)
                        {
                            if (!HeroListManager.Check("tahm-kench", enemy))
                            {
                                if (!_soulbound.UnderTurret(false) &&
                                    (_soulbound.Distance(enemy) > Player.AttackRange ||
                                     GameObjects.AllyHeroes.Where(
                                         a => a.NetworkId != _soulbound.NetworkId && a.NetworkId != Player.NetworkId)
                                         .Any(t => t.Distance(Player) > 600) ||
                                     GameObjects.AllyTurrets.Any(t => t.Distance(Player) < 600)))
                                {
                                    R.Cast();
                                }
                            }
                            return;
                        }
                    }
                }
            }
        }

        private void CheckSoulbound()
        {
            try
            {
                if (_soulbound == null)
                {
                    _soulbound =
                        GameObjects.AllyHeroes.FirstOrDefault(
                            a =>
                                a.Buffs.Any(
                                    b =>
                                        b.Caster.IsMe &&
                                        b.Name.Equals("kalistacoopstrikeally", StringComparison.OrdinalIgnoreCase)));
                    if (_soulbound != null)
                    {
                        IncomingDamageManager.Skillshots = true;
                        IncomingDamageManager.AddChampion(_soulbound);
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        protected override void OnPostUpdate() {}

        protected override void Combo()
        {
            var useQ = Menu.Item(Menu.Name + ".combo.q").GetValue<bool>() && Q.IsReady() &&
                       ResourceManager.Check("combo-q");
            var useE = Menu.Item(Menu.Name + ".combo.e").GetValue<bool>() && E.IsReady();

            if (useQ)
            {
                Casting.SkillShot(Q, Q.GetHitChance("combo"));
            }

            if (useE)
            {
                var target = TargetSelector.GetTarget(E);
                if (target != null && Rend.HasBuff(target))
                {
                    if (target.Distance(Player) > Orbwalking.GetRealAutoAttackRange(target))
                    {
                        if (
                            GameObjects.EnemyMinions.Any(
                                m =>
                                    m.IsValidTarget(Orbwalking.GetRealAutoAttackRange(m)) &&
                                    Rend.IsKillable(m, (m.HealthPercent < 25))))
                        {
                            E.Cast();
                        }
                        else
                        {
                            var minion =
                                GetDashObjects(
                                    GameObjects.EnemyMinions.Where(
                                        m => m.IsValidTarget(Orbwalking.GetRealAutoAttackRange(m)))
                                        .Select(e => e as Obj_AI_Base)
                                        .ToList())
                                    .Find(
                                        m =>
                                            m.Health > Player.GetAutoAttackDamage(m) &&
                                            m.Health < Player.GetAutoAttackDamage(m) + Rend.GetDamage(m, 1));
                            if (minion != null)
                            {
                                Orbwalker.ForceTarget(minion);
                            }
                        }
                    }
                    else if (E.IsInRange(target))
                    {
                        if (Rend.IsKillable(target, false))
                        {
                            E.Cast();
                        }
                        else
                        {
                            var buff = Rend.GetBuff(target);
                            if (buff != null &&
                                buff.Count >= Menu.Item(Menu.Name + ".combo.e-min").GetValue<Slider>().Value)
                            {
                                if (target.Distance(Player) > E.Range * 0.8 && !target.IsFacing(Player))
                                {
                                    E.Cast();
                                }
                            }
                        }
                    }
                }
            }

            if (Menu.Item(Menu.Name + ".combo.minions").GetValue<bool>() &&
                !GameObjects.EnemyHeroes.Any(
                    e => e.IsValidTarget() && e.Distance(Player) < Orbwalking.GetRealAutoAttackRange(e) * 1.1f) &&
                !Player.IsWindingUp && !Player.IsDashing())
            {
                var obj = GetDashObjects().FirstOrDefault();
                if (obj != null)
                {
                    Orbwalker.ForceTarget(obj);
                }
            }
            else
            {
                Orbwalker.ForceTarget(null);
            }
        }

        protected override void Harass()
        {
            if (Menu.Item(Menu.Name + ".harass.q").GetValue<bool>() && Q.IsReady() && ResourceManager.Check("harass-q"))
            {
                Casting.SkillShot(Q, Q.GetHitChance("harass"));
            }
            if (Menu.Item(Menu.Name + ".harass.e").GetValue<bool>() && E.IsReady() && ResourceManager.Check("harass-e"))
            {
                foreach (var enemy in GameObjects.EnemyHeroes.Where(e => E.IsInRange(e)))
                {
                    if (Rend.IsKillable(enemy, enemy.HealthPercent < 25))
                    {
                        E.Cast();
                    }
                    else
                    {
                        var buff = Rend.GetBuff(enemy);
                        if (buff != null &&
                            buff.Count >= Menu.Item(Menu.Name + ".harass.e-min").GetValue<Slider>().Value)
                        {
                            if (enemy.Distance(Player) > E.Range * 0.8 || buff.EndTime - Game.Time < 0.3)
                            {
                                E.Cast();
                            }
                        }
                    }
                }
            }
        }

        private List<Obj_AI_Base> QGetCollisions(Obj_AI_Hero source, Vector3 targetposition)
        {
            try
            {
                var input = new PredictionInput { Unit = source, Radius = Q.Width, Delay = Q.Delay, Speed = Q.Speed };
                input.CollisionObjects[0] = CollisionableObjects.Minions;
                return
                    Collision.GetCollision(new List<Vector3> { targetposition }, input)
                        .OrderBy(obj => obj.Distance(source))
                        .ToList();
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
            return new List<Obj_AI_Base>();
        }

        protected override void LaneClear()
        {
            if (!ResourceManager.Check("lane-clear"))
            {
                return;
            }

            var useQ = Menu.Item(Menu.Name + ".lane-clear.q").GetValue<bool>() && Q.IsReady();
            var useE = Menu.Item(Menu.Name + ".lane-clear.e").GetValue<bool>() && E.IsReady();

            if (!useQ && !useE)
            {
                return;
            }

            var minE = ItemData.Runaans_Hurricane_Ranged_Only.GetItem().IsOwned(Player) ? 3 : 2;
            var minQ = Menu.Item(Menu.Name + ".lane-clear.q-min").GetValue<Slider>().Value;
            var minions = MinionManager.GetMinions(Q.Range);
            if (minions.Count == 0)
            {
                return;
            }
            if (useQ && minions.Count >= minQ && !Player.IsWindingUp && !Player.IsDashing())
            {
                foreach (var minion in minions.Where(x => x.Health <= Q.GetDamage(x)))
                {
                    var killcount = 0;

                    foreach (var colminion in
                        QGetCollisions(Player, Player.ServerPosition.Extend(minion.ServerPosition, Q.Range)))
                    {
                        if (colminion.Health <= Q.GetDamage(colminion))
                        {
                            killcount++;
                        }
                        else
                        {
                            break;
                        }
                    }
                    if (killcount >= minQ)
                    {
                        Q.Cast(minion.ServerPosition);
                        break;
                    }
                }
            }
            if (useE)
            {
                var killable = minions.Where(m => E.IsInRange(m) && Rend.IsKillable(m, false)).ToList();
                if (killable.Count >= minE)
                {
                    E.Cast();
                }
            }
        }

        protected override void JungleClear()
        {
            if (!ResourceManager.Check("lane-clear") && !ResourceManager.IgnoreJungle("lane-clear"))
            {
                return;
            }

            var useQ = Menu.Item(Menu.Name + ".lane-clear.q").GetValue<bool>() && Q.IsReady();
            var useE = Menu.Item(Menu.Name + ".lane-clear.e").GetValue<bool>() && E.IsReady();

            if (!useQ && !useE)
            {
                return;
            }

            var minions = MinionManager.GetMinions(
                Q.Range, MinionTypes.All, MinionTeam.Neutral, MinionOrderTypes.MaxHealth);
            if (minions.Count == 0)
            {
                return;
            }
            if (useQ && minions.Count >= 1 && !Player.IsWindingUp && !Player.IsDashing())
            {
                foreach (var minion in minions.Where(x => x.Health <= Q.GetDamage(x)))
                {
                    var killcount = 0;

                    foreach (var colminion in
                        QGetCollisions(Player, Player.ServerPosition.Extend(minion.ServerPosition, Q.Range)))
                    {
                        if (colminion.Health <= Q.GetDamage(colminion))
                        {
                            killcount++;
                        }
                        else
                        {
                            break;
                        }
                    }
                    if (killcount >= 1)
                    {
                        Q.Cast(minion.ServerPosition);
                        break;
                    }
                }
            }
            if (useE)
            {
                var killable = minions.Where(m => E.IsInRange(m) && Rend.IsKillable(m, false)).ToList();
                if (killable.Count >= 1)
                {
                    E.Cast();
                }
            }
        }

        protected override void Flee()
        {
            Orbwalker.SetAttack(true);
            var dashObjects = GetDashObjects();
            if (dashObjects != null && dashObjects.Any())
            {
                Orbwalking.Orbwalk(dashObjects.First(), Game.CursorPos);
            }
        }

        protected override void Killsteal()
        {
            if (Menu.Item(Menu.Name + ".killsteal.e").GetValue<bool>() && E.IsReady() &&
                GameObjects.EnemyHeroes.Any(h => h.IsValidTarget(E.Range) && Rend.IsKillable(h, false)))
            {
                E.Cast();
            }
        }

        public static IOrderedEnumerable<Obj_AI_Base> GetDashObjects()
        {
            try
            {
                var objects =
                    GameObjects.EnemyMinions.Concat(GameObjects.Jungle)
                        .Where(o => o.IsValidTarget(Orbwalking.GetRealAutoAttackRange(o)))
                        .Select(o => o as Obj_AI_Base)
                        .ToList();
                var apexPoint = ObjectManager.Player.ServerPosition.To2D() +
                                (ObjectManager.Player.ServerPosition.To2D() - Game.CursorPos.To2D()).Normalized() *
                                Orbwalking.GetRealAutoAttackRange(ObjectManager.Player);
                return
                    objects.Where(
                        o =>
                            Utils.IsLyingInCone(
                                o.ServerPosition.To2D(), apexPoint, ObjectManager.Player.ServerPosition.To2D(), Math.PI))
                        .OrderBy(o => o.Distance(apexPoint, true));
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
            return null;
        }

        public static List<Obj_AI_Base> GetDashObjects(List<Obj_AI_Base> targets)
        {
            try
            {
                var apexPoint = ObjectManager.Player.ServerPosition.To2D() +
                                (ObjectManager.Player.ServerPosition.To2D() - Game.CursorPos.To2D()).Normalized() *
                                Orbwalking.GetRealAutoAttackRange(ObjectManager.Player);

                return
                    targets.Where(
                        o =>
                            Utils.IsLyingInCone(
                                o.ServerPosition.To2D(), apexPoint, ObjectManager.Player.ServerPosition.To2D(), Math.PI))
                        .OrderBy(o => o.Distance(apexPoint, true))
                        .ToList();
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
            return null;
        }

        internal class Rend
        {
            private static readonly float[] Damage = { 20, 30, 40, 50, 60 };
            private static readonly float[] DamageMultiplier = { 0.6f, 0.6f, 0.6f, 0.6f, 0.6f };
            private static readonly float[] DamagePerSpear = { 10, 14, 19, 25, 32 };
            private static readonly float[] DamagePerSpearMultiplier = { 0.2f, 0.225f, 0.25f, 0.275f, 0.3f };

            public static bool IsKillable(Obj_AI_Base target, bool check)
            {
                try
                {
                    if (check)
                    {
                        if (target.Health < 100 && target is Obj_AI_Minion)
                        {
                            if (HealthPrediction.GetHealthPrediction(target, 250 + Game.Ping / 2) <= 0)
                            {
                                return false;
                            }
                        }
                    }
                    var hero = target as Obj_AI_Hero;
                    if (hero != null)
                    {
                        if (Invulnerable.Check(hero, DamageType.Physical, false))
                        {
                            return false;
                        }
                    }
                    return GetDamage(target) > target.Health + target.PhysicalShield;
                }
                catch (Exception ex)
                {
                    Global.Logger.AddItem(new LogItem(ex));
                }
                return false;
            }

            private static float GetRealDamage(Obj_AI_Base target, float damage)
            {
                try
                {
                    if (target is Obj_AI_Minion)
                    {
                        var dragonBuff =
                            ObjectManager.Player.Buffs.FirstOrDefault(
                                b => b.Name.Equals("s5test_dragonslayerbuff", StringComparison.OrdinalIgnoreCase));
                        if (dragonBuff != null)
                        {
                            if (dragonBuff.Count == 4)
                            {
                                damage *= 1.15f;
                            }
                            else if (dragonBuff.Count == 5)
                            {
                                damage *= 1.3f;
                            }
                            if (target.CharData.BaseSkinName.StartsWith("SRU_Dragon"))
                            {
                                damage *= 1f - 0.07f * dragonBuff.Count;
                            }
                        }
                        if (target.CharData.BaseSkinName.StartsWith("SRU_Baron"))
                        {
                            var baronBuff =
                                ObjectManager.Player.Buffs.FirstOrDefault(
                                    b => b.Name.Equals("barontarget", StringComparison.OrdinalIgnoreCase));
                            if (baronBuff != null)
                            {
                                damage *= 0.5f;
                            }
                        }
                    }
                    damage -= target.HPRegenRate * 0.25f;
                    if (ObjectManager.Player.HasBuff("summonerexhaust"))
                    {
                        damage *= 0.6f;
                    }
                    return damage;
                }
                catch (Exception ex)
                {
                    Global.Logger.AddItem(new LogItem(ex));
                }
                return 0;
            }

            public static float GetDamage(Obj_AI_Hero target)
            {
                return GetDamage(target, -1);
            }

            public static float GetDamage(Obj_AI_Base target, int customStacks = -1)
            {
                return GetRealDamage(
                    target,
                    100 /
                    (100 + (target.Armor * ObjectManager.Player.PercentArmorPenetrationMod) -
                     ObjectManager.Player.FlatArmorPenetrationMod) * GetRawDamage(target, customStacks));
            }

            public static float GetRawDamage(Obj_AI_Base target, int customStacks = -1)
            {
                try
                {
                    var buff = GetBuff(target);
                    var eLevel = ObjectManager.Player.GetSpell(SpellSlot.E).Level;
                    if (buff != null || customStacks > -1)
                    {
                        var damage = (Damage[eLevel - 1] +
                                      DamageMultiplier[eLevel - 1] * ObjectManager.Player.TotalAttackDamage()) +
                                     ((customStacks < 0 && buff != null ? buff.Count : customStacks) - 1) *
                                     (DamagePerSpear[eLevel - 1] +
                                      DamagePerSpearMultiplier[eLevel - 1] *
                                      (ObjectManager.Player.BaseAttackDamage +
                                       ObjectManager.Player.FlatPhysicalDamageMod));
                        return damage;
                    }
                }
                catch (Exception ex)
                {
                    Global.Logger.AddItem(new LogItem(ex));
                }
                return 0f;
            }

            public static bool HasBuff(Obj_AI_Base target)
            {
                return GetBuff(target) != null;
            }

            public static BuffInstance GetBuff(Obj_AI_Base target)
            {
                return
                    target.Buffs.FirstOrDefault(
                        b =>
                            b.Caster.IsMe && b.IsValid &&
                            b.DisplayName.Equals("KalistaExpungeMarker", StringComparison.OrdinalIgnoreCase));
            }
        }
    }
}