using System.Collections.Generic;
using System.Linq;

namespace CityHighways.Core
{
    /// <summary>Итог проверки взяток перед подсчётом очков: у кого сколько нашли.</summary>
    public sealed class AuditResult
    {
        public int Player { get; }
        public int Checked { get; }
        public int Exposed { get; }
        public int Fine { get; }

        public AuditResult(int player, int @checked, int exposed, int fine)
        {
            Player = player;
            Checked = @checked;
            Exposed = exposed;
            Fine = fine;
        }
    }

    /// <summary>
    /// Финал партии.
    /// 1) Предупреждение: когда у кого-то остаётся FinalWarningPieces участков или меньше, всем объявляют «скоро финал».
    /// 2) Финальный заказ мэрии: в начале последнего круга мэрия выбирает одну свободную дорогу — ту, за которую
    ///    могут побороться больше всего компаний (их сеть рядом и хватает участков), при равенстве длиннее.
    ///    Кто построит её в последнем круге, получает FinalOrderBonus очков (как за цель мэрии).
    /// 3) Проверка перед итогами: каждая ещё нераскрытая взятка (последние два круга) раскрывается с шансом
    ///    FinalAuditPercent — иначе взятка в последнем круге ничем бы не рисковала.
    /// </summary>
    public sealed partial class Game
    {
        bool _finalWarned;
        readonly List<AuditResult> _audit = new List<AuditResult>();

        /// <summary>Объявлено ли «скоро финал»; растёт, когда объявили (для всплывающего сообщения).</summary>
        public bool FinalWarned => _finalWarned;
        public int FinalWarningNumber { get; private set; }

        /// <summary>Сколько ходов осталось до конца, считая текущий; 0 — последний круг ещё не начался.</summary>
        public int FinalTurnsLeft => _finalTurnsLeft > 0 ? _finalTurnsLeft : 0;

        /// <summary>Меньше всего участков у кого-то из игроков — сколько до последнего круга.</summary>
        public int FewestPieces => _players.Count == 0 ? 0 : _players.Min(p => p.RoadPieces);

        /// <summary>Дорога финального заказа (null — заказа нет) и кто его выполнил (-1 — пока никто).</summary>
        public string FinalOrderRoadId { get; private set; }
        public Road FinalOrderRoad => FinalOrderRoadId == null ? null : Map.GetRoad(FinalOrderRoadId);
        public int FinalOrderWinner { get; private set; } = -1;
        /// <summary>Растёт при открытии и при выполнении заказа (для всплывающих сообщений).</summary>
        public int FinalOrderNumber { get; private set; }

        /// <summary>Итоги проверки взяток перед подсчётом (пусто — проверять было нечего).</summary>
        public IReadOnlyList<AuditResult> FinalAudit => _audit;

        void CheckFinalWarning(PlayerState player)
        {
            if (_finalWarned || _finalTurnsLeft >= 0 || Config.FinalWarningPieces <= Config.FinalRoundPieces) return;
            if (player.RoadPieces > Config.FinalWarningPieces || player.RoadPieces <= Config.FinalRoundPieces) return;
            _finalWarned = true;
            FinalWarningNumber++;
            Emit($"Скоро финал: у компании {player.Name} осталось участков — {player.RoadPieces}. " +
                 $"Когда у кого-то останется {Config.FinalRoundPieces} или меньше, начнётся последний круг.");
        }

        /// <summary>Мэрия выбирает дорогу финального заказа (в начале последнего круга).</summary>
        void OpenFinalOrder()
        {
            if (Config.FinalOrderBonus <= 0 || FinalOrderRoadId != null) return;
            int maxPieces = _players.Max(p => p.RoadPieces);
            var touching = _players.Select(p =>
            {
                var spots = new HashSet<string>();
                foreach (var r in p.Roads)
                {
                    spots.Add(r.A);
                    spots.Add(r.B);
                }
                return spots;
            }).ToList();
            int Contenders(Road road) => _players.Count(p =>
                p.RoadPieces >= road.Length && (touching[p.Index].Contains(road.A) || touching[p.Index].Contains(road.B)));

            var road = Map.Roads
                .Where(r => !GetRoadOwner(r.Id).HasValue && IsRoadOpen(r.Id) && !IsUnderRepair(r.Id)
                            && r.Length >= 2 && r.Length <= maxPieces)
                .OrderByDescending(Contenders)
                .ThenByDescending(r => r.Length)
                .ThenBy(r => r.Id, System.StringComparer.Ordinal)
                .FirstOrDefault();
            if (road == null) return;
            FinalOrderRoadId = road.Id;
            FinalOrderNumber++;
            Emit($"Финальный заказ мэрии: дорога {Map.LocationName(road.A)} — {Map.LocationName(road.B)}. " +
                 $"Кто построит её в последнем круге, получит +{Config.FinalOrderBonus} очков.");
        }

        /// <summary>Построена дорога: не финальный ли это заказ.</summary>
        void CheckFinalOrder(PlayerState player, Road road)
        {
            if (FinalOrderRoadId == null || FinalOrderWinner >= 0 || road.Id != FinalOrderRoadId || _finalTurnsLeft <= 0) return;
            FinalOrderWinner = player.Index;
            FinalOrderNumber++;
            player.GoalPoints += Config.FinalOrderBonus;
            Emit($"{player.Name} выполняет финальный заказ мэрии: +{Config.FinalOrderBonus} очков.");
        }

        /// <summary>Проверка перед итогами: каждая нераскрытая взятка раскрывается с шансом FinalAuditPercent.</summary>
        void RunFinalAudit()
        {
            _audit.Clear();
            if (!BribesAllowed || _bribes.Count == 0 || Config.FinalAuditPercent <= 0) return;
            int fine = Config.BribeFineFor(_players.Count);
            foreach (var group in _bribes.GroupBy(b => b.Player).OrderBy(g => g.Key))
            {
                int total = group.Count();
                int exposed = 0;
                for (int i = 0; i < total; i++)
                    if (_rng.Next(100) < Config.FinalAuditPercent) exposed++;
                var player = _players[group.Key];
                if (exposed > 0)
                {
                    player.PenaltyPoints -= fine * exposed;
                    player.BribesExposed += exposed;
                }
                _audit.Add(new AuditResult(group.Key, total, exposed, fine * exposed));
            }
            int found = _audit.Sum(a => a.Exposed);
            string who = string.Join(", ", _audit.Where(a => a.Exposed > 0).Select(a => $"{_players[a.Player].Name} −{a.Fine}"));
            Emit(found > 0
                ? $"Проверка перед итогами: нашли взяток — {found} ({who})."
                : "Проверка перед итогами: взяток не нашли.");
            _bribes.Clear();
        }
    }
}
