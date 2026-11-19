using System;
using System.Collections.Generic;
using System.Linq;
using CityHighways.Core;

namespace CityHighways.Presentation
{
    /// <summary>Счётчики игры одним персонажем в профиле (для древа мастерства).</summary>
    [Serializable]
    public class CharacterProgress
    {
        public string kind;
        public int games;
        public int wins;
        /// <summary>Главный показатель персонажа: мосты у Мостовика, пошлины у Сборщика и т. д. (см. CharacterMastery.Metric).</summary>
        public int metric;
    }

    /// <summary>Узел древа мастерства.</summary>
    public sealed class MasteryNode
    {
        public string Id;
        public string Title;
        /// <summary>Что сделать (текст для карточки).</summary>
        public string Goal;
        /// <summary>Что нужно открыть раньше (id узлов этого же персонажа).</summary>
        public string[] Requires = Array.Empty<string>();
        public Func<CharacterProgress, int> Current;
        public int Target;
        /// <summary>Награда: влияние и/или вещь (id из Profiles.Catalog).</summary>
        public int Influence;
        public string Item;
        /// <summary>Место на схеме: колонка 0–3 и ряд (−1 — верхняя ветка, 0 — середина, 1 — нижняя).</summary>
        public int Column, Row;
    }

    /// <summary>
    /// Древо мастерства персонажей (вкладка профиля «Мастерство»). У каждого персонажа пять узлов:
    /// знакомство → ученик → две ветки (подмастерье — по главному показателю, с титулом персонажа; победитель —
    /// победы этим персонажем) → мастер (нужны обе ветки), за мастера — золотая рамка. Счётчики копятся после
    /// партий соревнования и кооператива, сыгранных этим персонажем; награды выдаются сразу, как узел открыт.
    /// </summary>
    public static class CharacterMastery
    {
        /// <summary>Главный показатель: как называется и пороги для ученика, подмастерья и мастера.</summary>
        public static (string label, string verb, int a, int b, int c) Metric(CharacterKind kind)
        {
            switch (kind)
            {
                case CharacterKind.Bridger: return ("мостов построено", "Постройте мостов", 2, 5, 10);
                case CharacterKind.Tollman: return ("пошлин собрано", "Соберите пошлин", 12, 40, 100);
                case CharacterKind.Official: return ("контрактов выполнено", "Выполните контрактов", 5, 15, 30);
                case CharacterKind.Foreman: return ("дорог построено", "Постройте дорог", 15, 40, 80);
                case CharacterKind.Insurer: return ("партий без провалов", "Сыграйте без проваленных контрактов партий", 2, 5, 10);
                case CharacterKind.Lucky: return ("очков набрано", "Наберите очков", 100, 300, 600);
                default: return ("", "", 1, 1, 1);
            }
        }

        /// <summary>Сколько партия добавляет к главному показателю.</summary>
        static int MetricOf(CharacterKind kind, GameFacts facts)
        {
            switch (kind)
            {
                case CharacterKind.Bridger: return facts.Bridges;
                case CharacterKind.Tollman: return facts.TollsEarned;
                case CharacterKind.Official: return facts.Completed;
                case CharacterKind.Foreman: return facts.RoadsBuilt;
                case CharacterKind.Insurer: return facts.Failed == 0 ? 1 : 0;
                case CharacterKind.Lucky: return Math.Max(0, facts.Total);
                default: return 0;
            }
        }

        /// <summary>Титул персонажа за ветку «Подмастерье» (только за мастерство, не продаётся и не выпадает).</summary>
        public static string TitleItem(CharacterKind kind)
        {
            switch (kind)
            {
                case CharacterKind.Lucky: return "title_m_lucky";
                case CharacterKind.Foreman: return "title_m_foreman";
                case CharacterKind.Insurer: return "title_m_insurer";
                case CharacterKind.Tollman: return "title_m_tollman";
                case CharacterKind.Official: return "title_m_official";
                case CharacterKind.Bridger: return "title_m_bridger";
                default: return null;
            }
        }

        public const string MasterFrame = "frame_gold";
        public const int WinsForWinner = 3;

        static string Key(CharacterKind kind) => kind.ToString();

        public static List<MasteryNode> Tree(CharacterKind kind)
        {
            var m = Metric(kind);
            string k = Key(kind);
            return new List<MasteryNode>
            {
                new MasteryNode { Id = k + ".1", Title = "Знакомство", Goal = "Сыграйте партию этим персонажем",
                    Current = p => p.games, Target = 1, Influence = 5, Column = 0, Row = 0 },
                new MasteryNode { Id = k + ".2", Title = "Ученик", Goal = $"{m.verb}: {m.a}", Requires = new[] { k + ".1" },
                    Current = p => p.metric, Target = m.a, Influence = 10, Column = 1, Row = 0 },
                new MasteryNode { Id = k + ".3", Title = "Подмастерье", Goal = $"{m.verb}: {m.b}", Requires = new[] { k + ".2" },
                    Current = p => p.metric, Target = m.b, Item = TitleItem(kind), Column = 2, Row = -1 },
                new MasteryNode { Id = k + ".4", Title = "Победитель", Goal = $"Победите этим персонажем {WinsForWinner} раза", Requires = new[] { k + ".2" },
                    Current = p => p.wins, Target = WinsForWinner, Influence = 20, Column = 2, Row = 1 },
                new MasteryNode { Id = k + ".5", Title = "Мастер", Goal = $"{m.verb}: {m.c}", Requires = new[] { k + ".3", k + ".4" },
                    Current = p => p.metric, Target = m.c, Item = MasterFrame, Influence = 0, Column = 3, Row = 0 },
            };
        }

        public static CharacterProgress Progress(PlayerProfile profile, CharacterKind kind)
        {
            if (profile.characterProgress == null) profile.characterProgress = new List<CharacterProgress>();
            string k = Key(kind);
            var progress = profile.characterProgress.FirstOrDefault(p => p.kind == k);
            if (progress == null)
            {
                progress = new CharacterProgress { kind = k };
                profile.characterProgress.Add(progress);
            }
            return progress;
        }

        public static bool Done(PlayerProfile profile, MasteryNode node) => profile.masteryDone != null && profile.masteryDone.Contains(node.Id);

        /// <summary>Узел доступен: всё, что перед ним, уже открыто.</summary>
        public static bool Available(PlayerProfile profile, MasteryNode node) => node.Requires.All(r => profile.masteryDone != null && profile.masteryDone.Contains(r));

        /// <summary>Сколько узлов персонажа открыто.</summary>
        public static int DoneCount(PlayerProfile profile, CharacterKind kind) => Tree(kind).Count(n => Done(profile, n));

        /// <summary>Итог партии: счётчики персонажа и новые узлы с наградами. Магнат и партии без персонажа не считаются.</summary>
        public static List<MasteryNode> AfterGame(PlayerProfile profile, GameFacts facts)
        {
            if (facts == null || facts.Magnate || facts.Character == CharacterKind.None || !Characters.All.Contains(facts.Character))
                return new List<MasteryNode>();
            var progress = Progress(profile, facts.Character);
            progress.games++;
            if (facts.Won) progress.wins++;
            progress.metric += MetricOf(facts.Character, facts);
            return Unlock(profile, facts.Character);
        }

        /// <summary>Открыть все узлы, условия которых выполнены (по порядку, чтобы цепочка открылась за раз), и выдать награды.</summary>
        public static List<MasteryNode> Unlock(PlayerProfile profile, CharacterKind kind)
        {
            var unlocked = new List<MasteryNode>();
            if (profile.masteryDone == null) profile.masteryDone = new List<string>();
            var progress = Progress(profile, kind);
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var node in Tree(kind))
                {
                    if (Done(profile, node) || !Available(profile, node) || node.Current(progress) < node.Target) continue;
                    profile.masteryDone.Add(node.Id);
                    Grant(profile, kind, node);
                    unlocked.Add(node);
                    changed = true;
                }
            }
            return unlocked;
        }

        static void Grant(PlayerProfile profile, CharacterKind kind, MasteryNode node)
        {
            string what = $"Мастерство · {Characters.Title(kind)} · {node.Title}";
            int influence = node.Influence;
            if (!string.IsNullOrEmpty(node.Item))
            {
                if (!profile.owned.Contains(node.Item)) profile.owned.Add(node.Item);
                // Рамка уже была (куплена раньше) — вместо неё её цена влиянием.
                else if (node.Item == MasterFrame) influence += Profiles.Item(MasterFrame)?.Price ?? 35;
            }
            if (influence > 0)
            {
                profile.wallet += influence;
                profile.earnedTotal += influence;
                Profiles.Record(profile, influence, "mastery", what);
            }
            else Profiles.Record(profile, 0, "mastery", what);
        }

        /// <summary>Текст награды узла для карточки.</summary>
        public static string RewardText(MasteryNode node)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(node.Item))
            {
                var item = Profiles.Item(node.Item);
                if (item != null) parts.Add(item.Kind == ShopKind.Title ? $"титул «{item.Name}»" : item.Name.ToLowerInvariant());
            }
            if (node.Influence > 0) parts.Add($"+{node.Influence} влияния");
            return parts.Count == 0 ? "" : string.Join(" и ", parts);
        }
    }
}
