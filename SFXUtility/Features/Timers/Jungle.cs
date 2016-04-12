#region License

/*
 Copyright 2014 - 2015 Nikita Bernthaler
 Jungle.cs is part of SFXUtility.

 SFXUtility is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 SFXUtility is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with SFXUtility. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion License

#region

using System;
using System.Collections.Generic;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SFXUtility.Classes;
using SFXUtility.Library;
using SFXUtility.Library.Extensions.NET;
using SFXUtility.Library.Extensions.SharpDX;
using SFXUtility.Library.Logger;
using SharpDX;
using SharpDX.Direct3D9;
#endregion

namespace SFXUtility.Features.Timers
{
    internal class Jungle : Child<Timers>
    {
        private float CheckInterval = 800f;
        private readonly List<Camp> _camps = new List<Camp>();
        private int _dragonStacks;
        private float _lastCheck = Environment.TickCount;
        private Font _mapText;
        private Font _minimapText;

        //buff names
        String
        chickenBuffName = "razorbeakalert",
        redBuffName = "blessingofthelizardelder",
        blueBuffName = "crestoftheancientgolem",
        golemBuffName = "krugstonefist",
        grompBuffName = "s5junglemushroomarmor";


        //camp names
        String
        chickenCampName = "SRU_Razorbeak",
        blueCampName = "SRU_Blue",
        redCampName = "SRU_Red",
        golemCampName = "SRU_Krug",
        grompCampName = "SRU_Gromp";


        //camps
        IEnumerable<Camp>
            redCamps, blueCamps, chickenCamps, golemCamps, grompCamps;

        public Jungle(Timers parent) : base(parent)
        {
            OnLoad();
        }

        protected override List<Utility.Map.MapType> BlacklistedMaps
        {
            get
            {
                return new List<Utility.Map.MapType>
                {
                    Utility.Map.MapType.CrystalScar,
                    Utility.Map.MapType.HowlingAbyss
                };
            }
        }

        private List<EnemyTracker> Enemies;

        public override string Name
        {
            get { return "Jungle"; }
        }

        protected override void OnEnable()
        {
            GameObject.OnCreate += OnGameObjectCreate;
            GameObject.OnDelete += OnGameObjectDelete;
            Game.OnUpdate += OnGameUpdate;
            Drawing.OnEndScene += OnDrawingEndScene;
            //Game.OnNotify += Game_OnNotify;

            
            base.OnEnable();
        }

        private void Game_OnNotify(GameNotifyEventArgs args)
        {
            if (args.EventId == GameEventId.OnChampionDie)
            {
                var killer = GameObjects.EnemyHeroes.FirstOrDefault(n => n.NetworkId == args.NetworkId);
                                
                if(killer != null)
                {
                    var eTracker = Enemies.FirstOrDefault(n => n.NetworkId == killer.NetworkId);
                    float buffRecentAllowedGap = 2;
                    if (killer == null || eTracker == null) return;

                    if (killer.HasBuff(blueBuffName))
                    {
                        var blue = killer.GetBuff(blueBuffName);
                        if (blue.StartTime < (Game.Time + buffRecentAllowedGap))
                            eTracker.ignoredBuffs.Add(blue);

                    }
                    if (killer.HasBuff(redBuffName))
                    {
                        var red = killer.GetBuff(redBuffName);
                        if (red.StartTime < (Game.Time + buffRecentAllowedGap))
                            eTracker.ignoredBuffs.Add(red);
                    }

                }

            }
        }

        protected override void OnDisable()
        {
            GameObject.OnCreate -= OnGameObjectCreate;
            GameObject.OnDelete -= OnGameObjectDelete;
            Game.OnUpdate -= OnGameUpdate;
            Drawing.OnEndScene -= OnDrawingEndScene;
            
            base.OnDisable();
        }

        private void OnGameObjectDelete(GameObject sender, EventArgs args)
        {
            try
            {
                if (!sender.IsValid || sender.Type != GameObjectType.obj_AI_Minion ||
                    sender.Team != GameObjectTeam.Neutral)
                {
                    return;
                }

                foreach (var camp in _camps.ToArray())
                {
                    var mob =
                        camp.Mobs.FirstOrDefault(m => m.Name.Contains(sender.Name, StringComparison.OrdinalIgnoreCase));
                    if (mob != null)
                    {
                        if (mob.Name.Contains("Herald", StringComparison.OrdinalIgnoreCase))
                        {
                            if (Game.Time + camp.RespawnTime > 20 * 60 ||
                                GameObjects.Jungle.Any(j => j.CharData.BaseSkinName.Contains("Baron")))
                            {
                                _camps.Remove(camp);
                                continue;
                            }
                        }
                        mob.Dead = true;
                        camp.Dead = camp.Mobs.All(m => m.Dead);
                        if (camp.Dead)
                        {
                            camp.Dead = true;
                            camp.NextRespawnTime = (int) Game.Time + camp.RespawnTime - 3;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnGameObjectCreate(GameObject sender, EventArgs args)
        {
            try
            {
                if (!sender.IsValid || sender.Type != GameObjectType.obj_AI_Minion ||
                    sender.Team != GameObjectTeam.Neutral)
                {
                    return;
                }

                foreach (var camp in _camps)
                {
                    var mob =
                        camp.Mobs.FirstOrDefault(m => m.Name.Contains(sender.Name, StringComparison.OrdinalIgnoreCase));
                    if (mob != null)
                    {
                        mob.Dead = false;
                        camp.Dead = false;
                        camp.ProbablyDead = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnGameUpdate(EventArgs args)
        {
            try
            {        
                CheckInterval = (float)Menu.Item(Name + "UpdateInterval").GetValue<Slider>().Value;
                
                if (_lastCheck + CheckInterval > Environment.TickCount)
                {
                    return;
                }
                _lastCheck = Environment.TickCount;
                
                var dragonStacks = 0;
                bool checkOnlyEnemyCamps = true;
                Func<Camp, bool> campCondition;

                if (checkOnlyEnemyCamps)
                    campCondition = n => n.Team != GameObjects.Player.Team;
                else
                    campCondition = n => true;
                
                
                foreach (var enemy in GameObjects.EnemyHeroes)
                { 
                    var buff =
                        enemy.Buffs.FirstOrDefault(
                            b => b.Name.Equals("s5test_dragonslayerbuff", StringComparison.OrdinalIgnoreCase));
                    if (buff != null)
                    {
                        dragonStacks = buff.Count;
                    }
                    var tracker = Enemies.First(n => n.NetworkId == enemy.NetworkId);
                    tracker.Update();

                    //bluebuff
                    buff = enemy.Buffs.FirstOrDefault(b => b.Name.Equals(blueBuffName, StringComparison.OrdinalIgnoreCase));
                    if (buff != null)
                    {
                        if (tracker.ignoredBuffs.Any(n => n.Name.StartsWith(blueBuffName)))
                            continue;
                        foreach (var camp in blueCamps.Where(campCondition))
                            if (!camp.Dead && !camp.ProbablyDead)
                            {
                                camp.ProbablyDead = true;
                                camp.NextRespawnTime = (int)buff.StartTime + camp.RespawnTime;

                                var otherCamp = blueCamps.FirstOrDefault(n => n.Team != camp.Team);
                                if (otherCamp != null && otherCamp.Dead && otherCamp.NextRespawnTime == camp.NextRespawnTime)
                                    camp.ProbablyDead = false;
                            }
                    }

                    //redbuff
                    buff = enemy.Buffs.FirstOrDefault(b => b.Name.Equals(redBuffName, StringComparison.OrdinalIgnoreCase));
                    if (buff != null)
                    {
                        if (tracker.ignoredBuffs.Any(n => n.Name.StartsWith(redBuffName)))
                            continue;

                        foreach (var camp in redCamps.Where(campCondition))
                            if (!camp.Dead && !camp.ProbablyDead)
                            {
                                camp.ProbablyDead = true;
                                camp.NextRespawnTime = (int)buff.StartTime + camp.RespawnTime;
                                
                                var otherCamp = redCamps.FirstOrDefault(n => n.Team != camp.Team);
                                if (otherCamp != null && otherCamp.Dead && otherCamp.NextRespawnTime == camp.NextRespawnTime)
                                    camp.ProbablyDead = false;
                            }
                    }

                    //chicken
                    buff = enemy.Buffs.FirstOrDefault(b => b.Name.Equals(chickenBuffName, StringComparison.OrdinalIgnoreCase));
                    if (buff != null)
                    {
                        foreach (var camp in chickenCamps.Where(campCondition))
                            if (!camp.Dead && !camp.ProbablyDead)
                            {
                                camp.ProbablyDead = true;
                                camp.NextRespawnTime = (int)buff.StartTime + camp.RespawnTime;

                                var otherCamp = chickenCamps.FirstOrDefault(n => n.Team != camp.Team);
                                if (otherCamp != null && otherCamp.Dead && otherCamp.NextRespawnTime == camp.NextRespawnTime)
                                    camp.ProbablyDead = false;
                            }
                    }

                    //golem
                    buff = enemy.Buffs.FirstOrDefault(b => b.Name.Equals(golemBuffName, StringComparison.OrdinalIgnoreCase));
                    if (buff != null)
                    {
                        foreach (var camp in golemCamps.Where(campCondition))
                            if (!camp.Dead && !camp.ProbablyDead)
                            {
                                camp.ProbablyDead = true;
                                camp.NextRespawnTime = (int)buff.StartTime + camp.RespawnTime;

                                var otherCamp = golemCamps.FirstOrDefault(n => n.Team != camp.Team);
                                if (otherCamp != null && otherCamp.Dead && otherCamp.NextRespawnTime == camp.NextRespawnTime)
                                    camp.ProbablyDead = false;
                            }
                    }

                    //gromp
                    buff = enemy.Buffs.FirstOrDefault(b => b.Name.Equals(grompBuffName, StringComparison.OrdinalIgnoreCase));
                    if (buff != null)
                    {
                        foreach (var camp in grompCamps.Where(campCondition))
                            if (!camp.Dead && !camp.ProbablyDead)
                            {
                                camp.ProbablyDead = true;
                                camp.NextRespawnTime = (int)buff.StartTime + camp.RespawnTime;

                                var otherCamp = grompCamps.FirstOrDefault(n => n.Team != camp.Team);
                                if (otherCamp != null && otherCamp.Dead && otherCamp.NextRespawnTime == camp.NextRespawnTime)
                                    camp.ProbablyDead = false;
                            }
                    }
                }

                foreach (var j in GameObjects.Jungle.Where(n => !n.IsDead && n.IsVisible))
                {
                    
                    if (redCamps.Any(n => n.ProbablyDead) && j.Name.StartsWith(redCampName))
                    {
                        var c = redCamps.FirstOrDefault(n => n.Mobs.Any(m => m.Name.StartsWith(j.Name)));
                        if(c == null)
                        {
                            Global.Logger.AddItem( new LogItem(new Exception("Jungle camp name not found " + j.Name)));
                            continue;
                        }
                        c.ProbablyDead = false;
                    }

                    if (blueCamps.Any(n => n.ProbablyDead) && j.Name.StartsWith(blueCampName))
                    {
                        var c = blueCamps.FirstOrDefault(n => n.Mobs.Any(m => m.Name.StartsWith(j.Name)));
                        if (c == null)
                        {
                            Global.Logger.AddItem(new LogItem(new Exception("Jungle camp name not found " + j.Name)));
                            continue;

                        }
                        c.ProbablyDead = false;
                    }

                    if(chickenCamps.Any(n => n.ProbablyDead) && j.Name.StartsWith(chickenCampName))
                    {
                        var c = chickenCamps.FirstOrDefault(n => n.Mobs.Any(m => m.Name.StartsWith(j.Name)));
                        if (c == null)
                        {
                            Global.Logger.AddItem(new LogItem(new Exception("Jungle camp name not found " + j.Name)));
                            continue;

                        }
                        c.ProbablyDead = false;
                    }

                    if (grompCamps.Any(n => n.ProbablyDead) && j.Name.StartsWith(grompCampName))
                    {
                        var c = grompCamps.FirstOrDefault(n => n.Mobs.Any(m => m.Name.StartsWith(j.Name)));
                        if (c == null)
                        {
                            Global.Logger.AddItem(new LogItem(new Exception("Jungle camp name not found " + j.Name)));
                            continue;

                        }
                        c.ProbablyDead = false;

                    }

                    if (golemCamps.Any(n => n.ProbablyDead) && j.Name.StartsWith(golemCampName))
                    {
                        var c = golemCamps.FirstOrDefault(n => n.Mobs.Any(m => m.Name.StartsWith(j.Name)));
                        if (c == null)
                        {
                            Global.Logger.AddItem(new LogItem(new Exception("Jungle camp name not found " + j.Name)));
                            continue;

                        }
                        c.ProbablyDead = false;
                    }
                }


                if (dragonStacks > _dragonStacks || dragonStacks == 5)
                {
                    var dCamp = _camps.FirstOrDefault(c => c.Mobs.Any(m => m.Name.Contains("Dragon")));
                    if (dCamp != null && !dCamp.Dead)
                    {
                        dCamp.Dead = true;
                        dCamp.NextRespawnTime = (int) Game.Time + dCamp.RespawnTime;
                    }
                }

                _dragonStacks = dragonStacks;

                var bCamp = _camps.FirstOrDefault(c => c.Mobs.Any(m => m.Name.Contains("Baron")));
                if (bCamp != null && !bCamp.Dead)
                {
                    var heroes = GameObjects.EnemyHeroes.Where(e => e.IsVisible);
                    foreach (var hero in heroes)
                    {
                        var buff =
                            hero.Buffs.FirstOrDefault(
                                b => b.Name.Equals("exaltedwithbaronnashor", StringComparison.OrdinalIgnoreCase));
                        if (buff != null)
                        {
                            bCamp.Dead = true;
                            bCamp.NextRespawnTime = (int) buff.StartTime + bCamp.RespawnTime;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnDrawingEndScene(EventArgs args)
        {
            try
            {
                if (Drawing.Direct3DDevice == null || Drawing.Direct3DDevice.IsDisposed)
                {
                    return;
                }

                var mapTotalSeconds = Menu.Item(Name + "DrawingMapTimeFormat").GetValue<StringList>().SelectedIndex == 1;
                var minimapTotalSeconds = Menu.Item(Name + "DrawingMinimapTimeFormat").GetValue<StringList>().SelectedIndex == 1;
                var mapEnabled = Menu.Item(Name + "DrawingMapEnabled").GetValue<bool>();
                var minimapEnabled = Menu.Item(Name + "DrawingMinimapEnabled").GetValue<bool>();

                if (!mapEnabled && !minimapEnabled)
                {
                    return;
                }

                foreach (var camp in _camps.Where(c => c.Dead || c.ProbablyDead))
                {
                    var color = (camp.ProbablyDead)? Color.Red : Color.White;
                    

                    if (camp.NextRespawnTime - Game.Time <= 0)
                    {
                        camp.Dead = false;
                        camp.ProbablyDead = false;
                        continue;
                    }

                    if (mapEnabled && camp.Position.IsOnScreen())
                    {
                        _mapText.DrawTextCentered(
                            (camp.NextRespawnTime - (int) Game.Time).FormatTime(mapTotalSeconds),
                            Drawing.WorldToScreen(camp.Position), color);
                    }
                    if (minimapEnabled)
                    {
                        _minimapText.DrawTextCentered(
                            (camp.NextRespawnTime - (int) Game.Time).FormatTime(minimapTotalSeconds),
                            camp.MinimapPosition, color);
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        protected sealed override void OnLoad()
        {
            try
            {
                Menu = new Menu(Name, Name);

                var drawingMenu = new Menu("Drawing", Name + "Drawing");
                var drawingMapMenu = new Menu("Map", drawingMenu.Name + "Map");
                var drawingMinimapMenu = new Menu("Minimap", drawingMenu.Name + "Minimap");

                drawingMapMenu.AddItem(
                    new MenuItem(drawingMapMenu.Name + "TimeFormat", "Time Format").SetValue(
                        new StringList(new[] { "mm:ss", "ss" })));
                drawingMapMenu.AddItem(
                    new MenuItem(drawingMapMenu.Name + "FontSize", "Font Size").SetValue(new Slider(20, 3, 30)));
                drawingMapMenu.AddItem(new MenuItem(drawingMapMenu.Name + "Enabled", "Enabled").SetValue(false));

                drawingMinimapMenu.AddItem(
                    new MenuItem(drawingMinimapMenu.Name + "TimeFormat", "Time Format").SetValue(
                        new StringList(new[] { "mm:ss", "ss" })));
                drawingMinimapMenu.AddItem(
                    new MenuItem(drawingMinimapMenu.Name + "FontSize", "Font Size").SetValue(new Slider(13, 3, 30)));
                drawingMinimapMenu.AddItem(new MenuItem(drawingMinimapMenu.Name + "Enabled", "Enabled").SetValue(false));

                drawingMenu.AddSubMenu(drawingMapMenu);
                drawingMenu.AddSubMenu(drawingMinimapMenu);

                Menu.AddSubMenu(drawingMenu);
                Menu.AddItem(new MenuItem(Name + "UpdateInterval", "Update Interval").SetValue(new Slider(800, 800, 2400)));

                Menu.AddItem(new MenuItem(Name + "Enabled", "Enabled").SetValue(false));

                Parent.Menu.AddSubMenu(Menu);

                _minimapText = MDrawing.GetFont(Menu.Item(Name + "DrawingMinimapFontSize").GetValue<Slider>().Value);
                _mapText = MDrawing.GetFont(Menu.Item(Name + "DrawingMapFontSize").GetValue<Slider>().Value);
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        protected override void OnInitialize()
        {
            try
            {
                _camps.AddRange(
                    Data.Jungle.Camps.Where(c => c.MapType == Utility.Map.GetMap().Type)
                        .Select(
                            c => new Camp(c.SpawnTime, c.RespawnTime, c.Position, c.Mobs, c.IsBig, c.MapType, c.Team)));

                redCamps = _camps.Where(c => c.Mobs.Any(m => m.Name.StartsWith(redCampName)));
                blueCamps = _camps.Where(c => c.Mobs.Any(m => m.Name.StartsWith(blueCampName)));
                chickenCamps = _camps.Where(c => c.Mobs.Any(m => m.Name.StartsWith(chickenCampName)));
                golemCamps = _camps.Where(c => c.Mobs.Any(m => m.Name.StartsWith(golemCampName)));
                grompCamps = _camps.Where(c => c.Mobs.Any(m => m.Name.StartsWith(grompCampName)));
                
                Enemies = new List<EnemyTracker>();
                foreach (var enemy in GameObjects.EnemyHeroes)
                {
                    var Tracker = new EnemyTracker { NetworkId = enemy.NetworkId };
                    Tracker.Update();
                    Enemies.Add(Tracker);
                }

                if (!_camps.Any())
                {
                    OnUnload(null, new UnloadEventArgs(true));
                    return;
                }

                base.OnInitialize();
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private class Camp : Data.Jungle.Camp
        {
            public Camp(float spawnTime,
                float respawnTime,
                Vector3 position,
                List<Data.Jungle.Mob> mobs,
                bool isBig,
                Utility.Map.MapType mapType,
                GameObjectTeam team,
                bool dead = false) : base(spawnTime, respawnTime, position, mobs, isBig, mapType, team)
            {
                ProbablyDead = false;
                Dead = dead;
                Mobs = mobs.Select(mob => new Mob(mob.Name)).ToList();
            }

            public new List<Mob> Mobs { get; private set; }
            public float NextRespawnTime { get; set; }
            public bool Dead
            {
                get;
                set;
            }
            private bool _probablydead;
            public bool ProbablyDead
            {
                get
                {
                    return this._probablydead && !this.Dead;
                }
                set
                {
                    this._probablydead = value;
                }
            }
        }

        private class EnemyTracker
        {
            


            public int NetworkId;
            public List<BuffInstance> ignoredBuffs;


            public EnemyTracker()
            {
                ignoredBuffs = new List<BuffInstance>();
            }

            public void Update()
            {
                foreach (var buff in ignoredBuffs)
                {
                    if (buff.EndTime > Game.Time)
                    {
                        ignoredBuffs.Remove(buff);
                    }
                }
            }

            
        }


        private class Mob : Data.Jungle.Mob
        {
            public Mob(string name, bool dead = false) : base(name)
            {
                Dead = dead;
            }

            public bool Dead { get; set; }
        }
    }
}