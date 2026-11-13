using System;
using System.Collections.Generic;

namespace CityHighways.Core
{
    internal static class ShuffleUtil
    {
        /// <summary>Тасование Фишера — Йейтса.</summary>
        public static void InPlace<T>(IList<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }

    /// <summary>Колода карт ресурсов со сбросом. Когда колода кончается, сброс тасуется и становится колодой.</summary>
    public sealed class ResourceDeck
    {
        readonly List<Resource> _drawPile = new List<Resource>();
        readonly List<Resource> _discardPile = new List<Resource>();
        readonly Random _rng;

        public int DrawPileCount => _drawPile.Count;
        public int DiscardPileCount => _discardPile.Count;
        public int AvailableCount => _drawPile.Count + _discardPile.Count;

        public ResourceDeck(IEnumerable<Resource> cards, Random rng)
        {
            _rng = rng;
            _drawPile.AddRange(cards);
            ShuffleUtil.InPlace(_drawPile, _rng);
        }

        ResourceDeck(Random rng)
        {
            _rng = rng;
        }

        internal static ResourceDeck Restore(IEnumerable<Resource> drawPile, IEnumerable<Resource> discardPile, Random rng)
        {
            var deck = new ResourceDeck(rng);
            deck._drawPile.AddRange(drawPile);
            deck._discardPile.AddRange(discardPile);
            return deck;
        }

        internal IReadOnlyList<Resource> DrawPile => _drawPile;
        internal IReadOnlyList<Resource> DiscardPile => _discardPile;

        public bool TryDraw(out Resource card)
        {
            if (_drawPile.Count == 0 && _discardPile.Count > 0)
            {
                _drawPile.AddRange(_discardPile);
                _discardPile.Clear();
                ShuffleUtil.InPlace(_drawPile, _rng);
            }

            if (_drawPile.Count == 0)
            {
                card = default(Resource);
                return false;
            }

            int last = _drawPile.Count - 1;
            card = _drawPile[last];
            _drawPile.RemoveAt(last);
            return true;
        }

        public void Discard(Resource card) => _discardPile.Add(card);

        /// <summary>Только для тестов: выбросить все карты.</summary>
        internal void Clear()
        {
            _drawPile.Clear();
            _discardPile.Clear();
        }
    }

    /// <summary>Колода контрактов. Возвращённые контракты уходят под низ.</summary>
    public sealed class ContractDeck
    {
        readonly List<Contract> _cards; // индекс 0 — верх колоды

        public int Count => _cards.Count;

        public ContractDeck(IEnumerable<Contract> contracts, Random rng)
        {
            _cards = new List<Contract>(contracts);
            ShuffleUtil.InPlace(_cards, rng);
        }

        ContractDeck(List<Contract> ordered)
        {
            _cards = ordered;
        }

        internal static ContractDeck Restore(IEnumerable<Contract> ordered) => new ContractDeck(new List<Contract>(ordered));

        internal IReadOnlyList<Contract> Cards => _cards;

        public List<Contract> Draw(int count)
        {
            var result = new List<Contract>();
            while (result.Count < count && _cards.Count > 0)
            {
                result.Add(_cards[0]);
                _cards.RemoveAt(0);
            }
            return result;
        }

        public void ReturnToBottom(IEnumerable<Contract> contracts) => _cards.AddRange(contracts);

        /// <summary>Положить контракты наверх колоды (новые контракты открывшегося района).</summary>
        public void PutOnTop(IEnumerable<Contract> contracts) => _cards.InsertRange(0, contracts);
    }
}
