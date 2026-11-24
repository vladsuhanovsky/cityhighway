using System;
using System.Collections.Generic;
using System.Linq;
using CityHighways.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>Карточки контрактов, выбор, закреплённые маршруты и подсветка карты.</summary>
    public sealed partial class GameView
    {
        void RefreshContracts()
        {
            _contractsBox.Clear();
            ClearContractHighlight();

            if (TryGetContractChooser(out var chooser)) BuildContractChoice(chooser);
            else
            {
                BuildContractList();
                RestorePinnedHighlight();
            }
        }

        bool TryGetContractChooser(out PlayerState chooser)
        {
            chooser = null;
            if (!HumanToAct) return false;
            if (_game.Phase != GamePhase.SetupContracts && _game.Phase != GamePhase.ChoosingContracts) return false;
            var player = _game.Players[_game.ActingPlayerIndex];
            if (player.PendingContracts.Count == 0 && player.CharacterOffer.Count == 0) return false;
            chooser = player;
            return true;
        }

        /// <summary>3D-герои в выборе персонажа (по одному на персонажа, переиспользуются при перестройке окна).</summary>
        readonly Dictionary<CharacterKind, Avatar3DStage> _heroStages = new Dictionary<CharacterKind, Avatar3DStage>();

        /// <summary>Крупный герой персонажа: вращающаяся 3D-модель или картинка; нет арта — нарисованный значок.</summary>
        VisualElement HeroOf(CharacterKind kind, Color color)
        {
            if (!Avatar3DCatalog.HasArt(kind))
            {
                var icon = new CharacterIcon(34f, kind, color);
                icon.style.marginRight = 10f;
                return icon;
            }
            if (!_heroStages.TryGetValue(kind, out var stage))
            {
                stage = new Avatar3DStage(84f, 122f);
                stage.Show(Avatar3DCatalog.For(kind));
                _heroStages[kind] = stage;
            }
            stage.RemoveFromHierarchy();
            stage.style.marginRight = 10f;
            stage.style.backgroundColor = new Color(color.r, color.g, color.b, 0.12f);
            Ui.Radius(stage, 10f);
            return stage;
        }

        /// <summary>Выбор компании в начале партии: предложенные персонажи с их способностями.</summary>
        VisualElement CharacterChoiceCard(PlayerState player)
        {
            var card = Ui.Card(12f);
            Ui.Border(card, 2f, Theme.Gold);
            card.style.marginBottom = 10f;
            card.Add(Ui.Text(LocalizationService.Get("game.ui.073"), 17f, Theme.Gold, true));
            var hint = Ui.Text(LocalizationService.Get("game.ui.074"), 12f, Theme.TextDim);
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.marginTop = 4f;
            hint.style.marginBottom = 8f;
            card.Add(hint);
            foreach (var kind in player.CharacterOffer)
            {
                var chosen = kind;
                var row = Ui.Row();
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 8f;
                row.style.backgroundColor = Theme.Panel;
                Ui.Radius(row, 8f);
                Ui.Padding(row, 8f, 10f);
                row.Add(HeroOf(kind, Palette.PlayerColor(player.Index)));
                var texts = Ui.Column();
                texts.style.flexGrow = 1f;
                texts.style.flexShrink = 1f;
                texts.Add(Ui.Text(Characters.Title(kind), 15f, Theme.Text, true));
                var ability = Ui.Text(Characters.Ability(kind, _game.Config), 12f, Theme.TextDim);
                ability.style.whiteSpace = WhiteSpace.Normal;
                texts.Add(ability);
                row.Add(texts);
                var pick = Ui.Button(LocalizationService.Get("game.ui.075"), () => Act(() => _game.ChooseCharacter(player.Index, chosen)), ButtonKind.Primary, 14f);
                pick.style.marginLeft = 10f;
                row.Add(pick);
                card.Add(row);
            }
            return card;
        }

        void BuildContractChoice(PlayerState player)
        {
            if (player.CharacterOffer.Count > 0) _contractsBox.Add(CharacterChoiceCard(player));
            if (player.PendingContracts.Count == 0) return;
            bool setup = _game.Phase == GamePhase.SetupContracts;
            int minKeep = setup ? _game.Config.InitialContractsMinKeep : _game.Config.ContractsMinKeep;
            int required = Math.Min(minKeep, player.PendingContracts.Count);

            string key = player.Index + ":" + string.Join(",", player.PendingContracts.Select(c => c.Id));
            if (key != _selectionKey)
            {
                _selectionKey = key;
                _selectedContracts.Clear();
                if (setup)
                    foreach (var c in player.PendingContracts) _selectedContracts.Add(c.Id);
            }

            var header = Ui.Row();
            header.style.flexShrink = 1f;
            header.style.alignItems = Align.FlexStart;
            header.style.flexWrap = Wrap.Wrap;
            header.style.marginBottom = 6f;
            var choiceTitle = Ui.Text(setup ? LocalizationService.Get("game.ui.076") : LocalizationService.Get("game.ui.077"),
                GameFxTuning.ContractsHeaderFontSize(Root.layout.width), Theme.Gold, true);
            choiceTitle.style.whiteSpace = WhiteSpace.Normal;
            choiceTitle.style.flexShrink = 1f;
            header.Add(choiceTitle);
            var hint = Ui.Text(EnglishUi ? $"  ·  Keep at least {required}. Click a card to select or remove it. Hover to highlight districts. Unfinished contracts cost points at game end." : $"  ·  оставьте хотя бы {required}. Клик по карточке — взять или отказаться. Наведите — районы подсветятся. Невыполненный контракт в конце отнимает очки.", 13f, Theme.TextDim);
            hint.style.flexShrink = 1f;
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.marginTop = 4f;
            header.Add(hint);
            _contractsBox.Add(header);

            var cards = player.PendingContracts.Select(contract => ContractCard(player.Index, contract, _choiceCarousel, true,
                _selectedContracts.Contains(contract.Id), () =>
                {
                    if (!_selectedContracts.Remove(contract.Id)) _selectedContracts.Add(contract.Id);
                    _app.Sound.Play(Sfx.Click, 0.5f);
                    _dirty = true;
                })).ToList();
            _choiceCarousel.SetCards(cards);
            _contractsBox.Add(_choiceCarousel);

            var confirm = Ui.Button(EnglishUi ? $"Take Selected ({_selectedContracts.Count})" : $"Взять выбранные ({_selectedContracts.Count})", () =>
            {
                var ids = _selectedContracts.ToList();
                if (ids.Count < required)
                {
                    Flash(EnglishUi ? $"You must keep at least {required} contracts." : $"Нужно оставить хотя бы {required}.");
                    return;
                }
                Act(() => setup ? _game.KeepInitialContracts(player.Index, ids) : _game.KeepContracts(ids));
            }, ButtonKind.Primary, 15f);
            confirm.style.marginTop = 6f;
            confirm.style.alignSelf = Align.FlexStart;
            Ui.SetEnabled(confirm, _selectedContracts.Count >= required);
            _contractsBox.Add(confirm);
        }

        void BuildContractList()
        {
            var viewer = _game.Players[Viewer];
            if (NeedsHandoff)
            {
                _contractsBox.Add(Ui.Text(LocalizationService.Get("game.ui.078"), 14f, Theme.TextDim));
                return;
            }
            if (viewer.Contracts.Count == 0)
            {
                _contractsBox.Add(Ui.Text(LocalizationService.Get("game.ui.079"), 14f, Theme.TextDim));
                return;
            }

            var tip = Ui.Text(LocalizationService.Get("game.ui.080"), 13f, Theme.TextDim);
            tip.style.whiteSpace = WhiteSpace.Normal;
            tip.style.marginBottom = 4f;
            _contractsBox.Add(tip);

            // Сначала невыполненные — о них важнее помнить.
            var checks = viewer.Contracts.Select(c => _game.CheckContract(viewer.Index, c)).OrderBy(c => c.Completed).ToList();
            _contractCarousel.SetCards(checks.Select(check =>
                ContractCard(viewer.Index, check.Contract, _contractCarousel, false, check.Completed, null)).ToList());
            _contractsBox.Add(_contractCarousel);
        }

        /// <summary>
        /// Карточка контракта: цветная кромка — статус (зелёная — выполнен своими дорогами, латунная — через чужие,
        /// у выбора — взят или нет), районы с метками цветов подсветки, очки крупно моноширинными цифрами.
        /// Наведение подсвечивает районы и путь на карте, клик закрепляет маршрут (или берёт контракт при выборе).
        /// </summary>
        VisualElement ContractCard(int playerIndex, Contract contract, CardCarousel carousel, bool selection, bool marked, Action onSelect)
        {
            var check = _game.CheckContract(playerIndex, contract);
            int foreign = check.ForeignRoads.Count;
            bool done = !selection && marked;
            bool pinned = !selection && IsPinned(playerIndex, contract);

            var card = Ui.Column();
            card.pickingMode = PickingMode.Position;
            card.style.width = ContractCardWidth;
            // Use minimum height rather than fixed height: English names and
            // long toll descriptions can grow without clipping the footer.
            card.style.minHeight = 172f;
            card.style.backgroundColor = pinned ? Theme.PanelHover : Theme.PanelLight;
            Ui.Radius(card, 12f);
            Ui.Padding(card, 12f, 14f);
            Ui.Border(card, pinned ? 2f : 1f, pinned ? Theme.Accent : Theme.Line);
            var edge = selection ? (marked ? Theme.Success : Theme.TextDim)
                : done ? (foreign > 0 ? Theme.Gold : Theme.Success)
                : Theme.PanelHover;
            card.style.borderTopWidth = 5f;
            card.style.borderTopColor = edge;
            Ui.Transitions(card, "translate", "background-color");

            var tags = Ui.Row();
            tags.style.flexShrink = 1f;
            tags.style.flexWrap = Wrap.Wrap;
            tags.style.alignItems = Align.Center;
            string kind = contract.Kind == ContractKind.Transit ? LocalizationService.Get("game.ui.081") : contract.Kind == ContractKind.Hub ? LocalizationService.Get("game.ui.082") : LocalizationService.Get("game.ui.083");
            tags.Add(Ui.Text(kind, 11f, contract.IsComplex ? MapView.ThirdColor : Theme.TextDim, true));
            var tagSpace = Ui.Box();
            tagSpace.style.flexGrow = 1f;
            tags.Add(tagSpace);
            if (_game.Players[playerIndex].IsMayorContract(contract.Id))
                tags.Add(Ui.Text(EnglishUi ? $"CITY HALL +{_game.Config.AuctionBonus}" : $"МЭРИЯ +{_game.Config.AuctionBonus}", 11f, Theme.Gold, true));
            if (selection && marked) tags.Add(Ui.Text(LocalizationService.Get("game.ui.084"), 11f, Theme.Success, true));
            card.Add(tags);

            VisualElement DistrictLine(Color chip, string prefix, string district)
            {
                var line = Ui.Row();
                line.style.alignItems = Align.FlexStart;
                line.style.marginTop = 6f;
                if (prefix != null)
                {
                    var p = Ui.Text(prefix, 13f, Theme.TextDim);
                    p.style.marginRight = 5f;
                    line.Add(p);
                }
                line.Add(DistrictChip(chip));
                var name = Ui.Text(DistrictName(district), 16f, done ? Theme.TextDim : Theme.Text, true);
                name.style.flexShrink = 1f;
                name.style.whiteSpace = WhiteSpace.Normal;
                line.Add(name);
                return line;
            }
            card.Add(DistrictLine(MapView.FromColor, null, contract.DistrictA));
            card.Add(DistrictLine(MapView.ToColor, null, contract.DistrictB));
            if (contract.IsComplex)
                card.Add(DistrictLine(MapView.ThirdColor, contract.Kind == ContractKind.Transit ? LocalizationService.Get("game.ui.085") : LocalizationService.Get("game.ui.086"), contract.DistrictC));

            var space = Ui.Box();
            space.style.flexGrow = 1f;
            card.Add(space);

            var bottom = Ui.Row();
            bottom.style.alignItems = Align.FlexEnd;
            bottom.style.marginTop = 8f;
            string status;
            Color statusColor = Theme.TextDim;
            if (selection) status = marked ? LocalizationService.Get("game.ui.087") : LocalizationService.Get("game.ui.088");
            else if (done && foreign > 0)
            {
                status = EnglishUi ? $"via {foreign} rival roads · toll −{check.Toll}" + (check.JamFee > 0 ? $" · jam −{check.JamFee}" : "") : $"через {foreign} чужих · проезд −{check.Toll}" + (check.JamFee > 0 ? $" · пробка −{check.JamFee}" : "");
                statusColor = Theme.Gold;
            }
            else if (done)
            {
                status = LocalizationService.Get("game.ui.089");
                statusColor = Theme.Success;
            }
            else if (contract.Kind == ContractKind.Hub && check.LegsCompleted > 0)
                status = EnglishUi ? $"completed {check.LegsCompleted} of {check.LegsTotal}" : $"готово {check.LegsCompleted} из {check.LegsTotal}";
            else status = LocalizationService.Get("game.ui.090");
            var statusLabel = Ui.Text(pinned ? status + (EnglishUi ? "\nroute pinned" : "\nмаршрут закреплён") : status, 12f, statusColor);
            statusLabel.style.flexGrow = 1f;
            statusLabel.style.flexShrink = 1f;
            statusLabel.style.whiteSpace = WhiteSpace.Normal;
            bottom.Add(statusLabel);
            bottom.Add(Ui.Number(done ? "+" + contract.Points : contract.Points.ToString(), 26f, done ? Theme.Success : Theme.Gold, true));
            card.Add(bottom);

            card.RegisterCallback<PointerEnterEvent>(_ =>
            {
                card.style.translate = new Translate(new Length(0f),
                    new Length(Ui.ReducedMotion ? 0f : -4f));
                ShowContractHighlight(playerIndex, contract);
            });
            card.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                card.style.translate = new Translate(new Length(0f), new Length(0f));
                // Уводим курсор — возвращается закреплённый маршрут (если есть), а не пустая карта.
                if (!selection) RestorePinnedHighlight();
                else ClearContractHighlight();
            });
            void ActivateContract()
            {
                if (selection)
                {
                    onSelect?.Invoke();
                    return;
                }
                if (IsPinned(playerIndex, contract)) _pinnedContracts.Remove(playerIndex);
                else _pinnedContracts[playerIndex] = contract.Id;
                _app.Sound.Play(Sfx.Click, 0.5f);
                _dirty = true;
            }
            card.RegisterCallback<ClickEvent>(_ =>
            {
                if (carousel != null && carousel.WasDragged) return;
                ActivateContract();
            });
            GamepadSelectable.Attach(card, ActivateContract,
                () => ShowContractHighlight(playerIndex, contract),
                () =>
                {
                    if (!selection) RestorePinnedHighlight();
                    else ClearContractHighlight();
                });
            return card;
        }

        bool IsPinned(int playerIndex, Contract contract) =>
            _pinnedContracts.TryGetValue(playerIndex, out var id) && id == contract.Id;

        /// <summary>
        /// Вернуть закреплённую подсветку текущего зрителя. Путь пересчитывается заново — после постройки
        /// своих участков он сразу становится зелёным. Если контракта у игрока больше нет, закрепление снимается.
        /// </summary>
        void RestorePinnedHighlight()
        {
            ClearContractHighlight();
            if (_game == null || NeedsHandoff || TryGetContractChooser(out _)) return;
            var viewer = _game.Players[Viewer];
            if (!_pinnedContracts.TryGetValue(viewer.Index, out var id)) return;
            var contract = viewer.Contracts.FirstOrDefault(c => c.Id == id);
            if (contract == null)
            {
                _pinnedContracts.Remove(viewer.Index);
                return;
            }
            ShowContractHighlight(viewer.Index, contract);
        }

        /// <summary>
        /// «● Район А — ● Район Б»: метки тех же цветов, что и подсветка на карте (жёлтый, красный).
        /// Транзит: «… через ● В», узел: «… и ● В»; третий район фиолетовый.
        /// </summary>
        VisualElement DistrictPair(Contract contract, Color textColor)
        {
            var line = Ui.Row();
            line.style.flexWrap = Wrap.Wrap;
            line.style.alignItems = Align.Center;
            line.Add(DistrictChip(MapView.FromColor));
            line.Add(Ui.Text(DistrictName(contract.DistrictA), 14f, textColor));
            line.Add(Ui.Text(" — ", 14f, Theme.TextDim));
            line.Add(DistrictChip(MapView.ToColor));
            line.Add(Ui.Text(DistrictName(contract.DistrictB), 14f, textColor));
            if (contract.IsComplex)
            {
                line.Add(Ui.Text(EnglishUi ? (contract.Kind == ContractKind.Transit ? " via " : " and ") : (contract.Kind == ContractKind.Transit ? " через " : " и "), 14f, Theme.TextDim));
                line.Add(DistrictChip(MapView.ThirdColor));
                line.Add(Ui.Text(DistrictName(contract.DistrictC), 14f, textColor));
            }
            return line;
        }

        /// <summary>Название контракта одной строкой: «А — Б», «А — Б через В» или «А — Б и В».</summary>
        string ContractTitle(Contract contract)
        {
            string title = $"{DistrictName(contract.DistrictA)} — {DistrictName(contract.DistrictB)}";
            if (contract.Kind == ContractKind.Transit) title += (EnglishUi ? " via " : " через ") + DistrictName(contract.DistrictC);
            else if (contract.Kind == ContractKind.Hub) title += (EnglishUi ? " and " : " и ") + DistrictName(contract.DistrictC);
            return title;
        }

        static VisualElement DistrictChip(Color color)
        {
            var chip = Ui.Box();
            chip.style.width = 9f;
            chip.style.height = 9f;
            chip.style.marginRight = 5f;
            chip.style.backgroundColor = color;
            Ui.Radius(chip, 4.5f);
            return chip;
        }

        /// <summary>Подсветить районы контракта и найденный путь: свои дороги — зелёным, чужие — оранжевым.</summary>
        void ShowContractHighlight(int playerIndex, Contract contract)
        {
            ClearContractHighlight();
            _map.SetContractHighlight(contract.DistrictA, contract.DistrictB, contract.DistrictC);

            var check = _game.CheckContract(playerIndex, contract);
            foreach (var road in check.Path)
            {
                bool mine = _game.GetRoadOwner(road.Id) == playerIndex;
                _map.PathRoads[road.Id] = mine ? new Color(0.30f, 0.85f, 0.40f, 0.95f) : new Color(1f, 0.55f, 0.10f, 0.95f);
            }
            _map.Repaint();
        }

        void ClearContractHighlight()
        {
            _map.ClearContractHighlight();
        }

    }
}
