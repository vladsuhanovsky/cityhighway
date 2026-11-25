using System;
using System.Linq;
using CityHighways.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Рейд на рекорд: партия по начальным условиям призрака (та же карта, зерно колоды, соперники, компании) —
    /// вы садитесь на его место. Вверху — значок призрака: его итог и сколько у него было к этому кругу
    /// («Влад: 97 · к 8-му кругу у него 31, у вас 34»). В конце — побит рекорд или нет.
    /// </summary>
    public sealed partial class GameView
    {
        Ghost _ghost;
        Label _ghostChip;

        /// <summary>Призрак этой партии (рейд); null — обычная партия.</summary>
        public void SetGhost(Ghost ghost)
        {
            _ghost = ghost;
            if (_ghost == null) return;
            if (_ghostChip == null)
            {
                _ghostChip = Ui.Text("", 13f, new Color(0.78f, 0.86f, 1f), true);
                _ghostChip.style.backgroundColor = new Color(0.36f, 0.45f, 0.75f, 0.28f);
                Ui.Radius(_ghostChip, 14f);
                Ui.Padding(_ghostChip, 6f, 12f);
                _ghostChip.style.marginRight = 10f;
                _ghostChip.style.borderLeftWidth = 3f;
                _ghostChip.style.borderLeftColor = new Color(0.62f, 0.74f, 1f);
                _ghostChip.tooltip = LocalizationService.Language == "en" ? "Record Raid: play on the same map, with the same deck and opponents as the record holder. Compare roads, goals and luck at each round; contracts count at the end." : "Рейд: те же карта, колода и соперники, что в партии рекордсмена. Сравнение — дороги, цели и удача к началу круга; " +
                                     "контракты считаются в конце.";
                int index = _topBar.IndexOf(_logTab);
                if (index >= 0) _topBar.Insert(index, _ghostChip);
                else _topBar.Add(_ghostChip);
            }
            RefreshGhost();
        }

        /// <summary>Начать рейд: партия с начальными условиями из повтора призрака, вы — на его месте.</summary>
        public void StartRaid(GameStartInfo start)
        {
            Game game;
            try
            {
                var map = CityHighwaysApp.LoadMap(_setup.mapResourcePath);
                var characters = (start.characters ?? new int[start.players.Length]).Select(c => (CharacterKind)c).ToList();
                game = new Game(map, _setup.PlayerNames(), start.config, start.seed, characters);
                _ghost?.BuildTimeline(map);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                ShowError(e.Message);
                return;
            }
            string who = _ghost == null ? "рекордсмена" : _ghost.Source == "self" ? "свой рекорд" : _ghost.Name;
            Begin(game, 0f, LocalizationService.Language == "en" ? $"Record Raid ({(_ghost == null ? "record holder" : _ghost.Source == "self" ? "your record" : _ghost.Name)}: {_ghost?.Score ?? 0} pts). Same map, deck and opponents; your moves decide the outcome." : $"Рейд на рекорд ({who}: {_ghost?.Score ?? 0} очк.) — те же карта, колода и соперники. Ваши ходы решают всё.");
        }

        int RaidSeat()
        {
            if (_game == null) return -1;
            int seat = _ghost?.Replay?.authorSeat ?? -1;
            if (seat >= 0 && seat < _game.Players.Count) return seat;
            for (int i = 0; i < _setup.players.Count && i < _game.Players.Count; i++)
                if (!_setup.players[i].isBot) return i;
            return -1;
        }

        void RefreshGhost()
        {
            if (_ghostChip == null || _ghost == null || _game == null) return;
            bool english = LocalizationService.Language == "en";
            _ghostChip.tooltip = english ? "Record Raid: same map, deck and opponents as the record holder." : "Рейд на рекорд: та же карта, колода и соперники.";
            string name = english ? (_ghost.Source == "self" ? "Your record" : $"Ghost {_ghost.Name}") : (_ghost.Source == "self" ? "Ваш рекорд" : $"Призрак {_ghost.Name}");
            string text = $"{name}: {_ghost.Score}";
            int seat = RaidSeat();
            var timeline = _ghost.ScoreByRound;
            if (timeline != null && seat >= 0 && _game.Phase != GamePhase.SetupContracts && _game.Phase != GamePhase.GameOver)
            {
                int round = (_game.TurnNumber - 1) / Math.Max(1, _game.Players.Count) + 1;
                int theirs = timeline[Mathf.Clamp(round, 0, timeline.Length - 1)];
                var me = _game.Players[seat];
                int mine = me.RoadPoints + me.GoalPoints + me.LuckPoints;
                text += english ? $" · round {round}: record {theirs}, yours {mine}" : $" · к {round}-му кругу: {theirs}, у вас {mine}";
                _ghostChip.style.color = mine >= theirs ? new Color(0.62f, 0.95f, 0.66f) : new Color(1f, 0.78f, 0.62f);
            }
            if (timeline == null)
            {
                _ghostChip.style.color = new Color(0.78f, 0.86f, 1f);
                _ghostChip.tooltip += english ? " Round-by-round comparison unavailable." : " Сравнение по кругам недоступно.";
            }
            _ghostChip.text = text;
        }

        /// <summary>Партия рейда закончилась: побит ли рекорд.</summary>
        void RaidFinished(ReplayFile replay)
        {
            if (_ghost == null || _game?.Result == null) return;
            int seat = RaidSeat();
            if (seat < 0) return;
            int mine = _game.Result.Ranking.FirstOrDefault(s => s.PlayerIndex == seat)?.Total ?? 0;
            string who = _ghost.Source == "self" ? "свой рекорд" : $"рекорд {_ghost.Name}";
            bool beaten = mine > _ghost.Score;
            string text = LocalizationService.Language == "en"
                ? (beaten ? $"Record beaten! {mine} versus {_ghost.Score} points." : mine == _ghost.Score ? $"Tied the record at {mine} points." : $"Record Raid lost: {_ghost.Score - mine} points short ({mine} of {_ghost.Score}).")
                : (beaten ? $"Рейд удался: {who} побит — {mine} против {_ghost.Score}!" : mine == _ghost.Score ? $"Ничья с призраком: по {mine} очк." : $"Рейд не удался: до рекорда не хватило {_ghost.Score - mine} очк. ({mine} из {_ghost.Score}).");
            Toast(text, beaten ? Theme.Success : Theme.Gold);
            if (beaten) _app.Sound.Play(Sfx.Bell, 0.8f);
            SocialCloud.News.Add(text);
            _ghostChip?.RemoveFromHierarchy();
            _ghostChip = null;
        }
    }
}
