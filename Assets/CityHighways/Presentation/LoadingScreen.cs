using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Экран загрузки партии (магнат, соревнование, кооператив): пока готовится карта и строится 3D, видно, что происходит.
    /// Шаги выполняются по одному за кадр (между ними интерфейс успевает перерисоваться): полоса растёт,
    /// подпись меняется, по полосе едет фура цвета режима, внизу — советы по правилам режима (сменяются каждые 5 с).
    /// Фон — картина режима из Scenario с медленным приближением, после первого шага под заголовком — название города.
    /// Готовые наборы: <see cref="Magnate"/>, <see cref="Competition"/>, <see cref="Cooperative"/>.
    /// Ошибка на любом шаге — сообщение и кнопка «В меню».
    /// </summary>
    public sealed class LoadingScreen
    {
        public VisualElement Root { get; }
        readonly Label _title, _subtitle, _stage, _tip;
        readonly VisualElement _bar, _truck, _art;
        readonly string[] _tips;
        readonly string _failText;
        readonly Color _accent;
        int _tipIndex;
        float _time;

        public static readonly string[] MagnateTips =
        {
            "Скоропорт приносит много очков, но за каждый лишний раунд в пути теряет по 2.",
            "Тяжеловес едет только по широким дорогам: бетон, мосты и магистрали.",
            "Опасный груз в Центре требует взятки влиянием — иначе его конфискуют.",
            "Если на дорогу за раунд заехало больше фур, чем она пропускает, — пробка: все стоят и теряют очки.",
            "Своя дорога — бесплатный проезд для ваших фур и плата с чужих.",
            "Своя дорога, кликнутая ещё раз, становится магистралью: больше фур и тяжеловесы.",
            "Маршруты планируются тайно, а фуры едут разом — угадайте, где соберётся пробка.",
            "Первый раунд — раунд стройки: посмотрите, где лежат грузы, и проложите дорогу к ним.",
            "Хабом владеет тот, кто вложил в него больше очков стройки, — хаб можно перехватить.",
            "Отстающие ходят и грузятся первыми, а компания с наименьшим счётом получает +1 очко стройки.",
            "По своей дороге длиной от 2 участков фура едет на участок быстрее.",
            "Если груз увёл соперник, фура сама возьмёт другой с того же хаба.",
            "Второй, кто выполнит заказ мэрии, получает половину награды.",
            "Пустая фура ездит по любым дорогам — её можно заранее отправить к тяжеловесу.",
            "Тупики и единственные переправы строят только игроки — это ценные участки.",
        };

        static readonly string[] MagnateTipsEnglish =
        {
            "Perishable cargo scores highly, but loses value for every extra round in transit.",
            "Heavy cargo can only use wide roads: concrete roads, bridges and highways.",
            "Hazardous cargo in the Centre requires an influence bribe or it will be confiscated.",
            "When too many trucks enter a road in one round, they all get stuck and lose points.",
            "Your roads are free for your trucks and generate tolls from rivals.",
            "Upgrade your road to a highway for more capacity and heavy cargo access.",
            "Plan routes secretly; trucks move together. Predict where traffic jams will form.",
            "The opening round is for construction: inspect cargo locations and build toward them.",
            "The company investing the most construction points owns the hub, which can change hands.",
            "Trailing companies act and load first; the lowest-scoring company gets a construction bonus.",
            "Your trucks travel faster on your own roads that are two or more segments long.",
            "If a rival takes a shipment, your truck tries another shipment at the same hub.",
            "The second company to finish a city order earns half the reward.",
            "Empty trucks can use any road, including narrow roads, to reach heavy cargo.",
            "Dead ends and sole river crossings are valuable roads only players can build.",
        };

        public static readonly string[] CompetitionTips =
        {
            "Длинные дороги приносят больше очков: дорога из 6 участков выгоднее трёх по 2.",
            "Цвет дороги — её ресурс. Белую дорогу можно построить картами любого одного цвета.",
            "Техника заменяет любую карту, но открытую технику можно взять только первой картой — и это весь ход.",
            "Невыполненный контракт в конце партии отнимает очки — не набирайте лишних.",
            "Контракт засчитается и через чужие дороги, если на пути есть хотя бы одна ваша, — но владельцу придётся заплатить.",
            "Соперники платят вам за проезд по вашим дорогам — стройте там, где все захотят ехать.",
            "Свою дорогу можно улучшить до магистрали: больше очков и дороже проезд для соперников.",
            "Загруженная дорога встаёт в пробку — объезд может оказаться выгоднее.",
            "Сделка не тратит ход: раз в ход предложите сопернику обмен картами.",
            "Пропуск, проданный в сделке, позволяет сопернику до конца партии ездить по вашим дорогам бесплатно.",
            "Влияние в очки не превращается — тратьте его во время партии.",
            "Цель мэрии достаётся только первому, кто её выполнит.",
            "Когда у кого-то остаётся мало участков дороги, начинается последний круг — следите за счётчиком.",
            "Взятка в последнем круге — риск: перед итогами нераскрытые взятки могут всплыть.",
            "Карта удачи может принести и баф, и дебаф — под кнопкой «Удача» видно, что осталось в колоде.",
        };

        static readonly string[] CompetitionTipsEnglish =
        {
            "Long roads score more than several short ones of the same total length.",
            "A road's colour shows its resource. White roads accept any single resource type.",
            "Machinery substitutes for any resource, but taking face-up machinery ends your turn.",
            "Unfinished contracts cost points at game end. Do not keep too many.",
            "A contract can use rival roads if it includes your own, but you pay their owners.",
            "Rivals pay tolls when they travel your roads. Build busy connections.",
            "Upgrade your roads to highways for more points and higher tolls.",
            "Traffic jams make detours worthwhile.",
            "Trading cards once per turn does not consume your action.",
            "A sold pass lets the buyer travel your roads for free until game end.",
            "Influence does not convert into points. Spend it during the game.",
            "Only the first player to achieve a City Hall goal receives its reward.",
            "The final round begins when someone runs low on road pieces.",
            "Bribes made near game end may be discovered in the final audit.",
            "Luck cards can help or hurt; check how many remain in the deck.",
        };

        public static readonly string[] CooperativeTips =
        {
            "Все компании — одна команда, противник — сам город. Побеждаете или проигрываете вместе.",
            "Сеть общая: дорога любого союзника засчитывается в ваши заказы, и проезд по ней бесплатный.",
            "Кризис растёт сам каждые несколько кругов, а ремонт, снегопад и парад добавляют ещё.",
            "Если кризис дойдёт до предела — команда проиграла. Не тяните с заказами.",
            "Сделки помогают делиться картами: отдайте союзнику ресурс, которого ему не хватает.",
            "Взяток, аукциона и пропусков в кооперативе нет — только общая стройка.",
            "Договоритесь, кто какой район тянет, чтобы не строить две дороги в одно место.",
            "Длинная общая магистраль закрывает сразу несколько заказов.",
            "Чем больше игроков, тем больше заказов нужно выполнить для победы.",
            "Когда у кого-то кончаются участки, начинается последний круг — заказов к этому времени должно хватить.",
        };

        static readonly string[] CooperativeTipsEnglish =
        {
            "All companies form one team against the city. You win or lose together.",
            "Roads are shared: each ally's roads count for city orders and carry no tolls.",
            "Crisis rises over time, with repairs, snow and parades adding more.",
            "If crisis reaches the limit, everyone loses. Finish orders early.",
            "Trade the resources your teammates need.",
            "Co-op has no bribes, auctions or passes: work together on construction.",
            "Coordinate which districts you connect so you do not duplicate routes.",
            "A long shared highway can complete multiple city orders.",
            "More players means more orders are needed for victory.",
            "When anyone runs out of road pieces, the final round starts; finish enough orders first.",
        };

        public static LoadingScreen Magnate() =>
            new LoadingScreen(LocalizationService.Language == "en" ? "Logistics Magnate" : "Логистический магнат",
                LocalizationService.Language == "en" ? "Generating a new city" : "Генерируем новый город",
                LocalizationService.Language == "en" ? MagnateTipsEnglish : MagnateTips,
                GeneratedArt.MagnateLoading, GameColors.Accent, LocalizationService.Language == "en" ? "Could not generate the city: " : "Не удалось построить город: ");

        public static LoadingScreen Competition(string title = "Соревнование") =>
            new LoadingScreen(LocalizationService.Language == "en" && title == "Соревнование" ? "Competitive" : title,
                LocalizationService.Language == "en" ? "Preparing the game" : "Готовим партию",
                LocalizationService.Language == "en" ? CompetitionTipsEnglish : CompetitionTips,
                GeneratedArt.CompetitionLoading, GameColors.Accent, LocalizationService.Language == "en" ? "Could not start the game: " : "Не удалось начать партию: ");

        public static LoadingScreen Cooperative() =>
            new LoadingScreen(LocalizationService.Language == "en" ? "Cooperative" : "Кооператив",
                LocalizationService.Language == "en" ? "Assembling your team" : "Собираем команду против города",
                LocalizationService.Language == "en" ? CooperativeTipsEnglish : CooperativeTips,
                GeneratedArt.CooperativeLoading, Theme.Success, LocalizationService.Language == "en" ? "Could not start the game: " : "Не удалось начать партию: ");

        public LoadingScreen(string title, string subtitle, string[] tips, string artPath, Color accent, string failText)
        {
            _tips = tips != null && tips.Length > 0 ? tips : MagnateTips;
            _accent = accent;
            _failText = failText ?? (LocalizationService.Language == "en" ? "Error: " : "Ошибка: ");
            Root = new VisualElement();
            Ui.Fill(Root);
            Root.style.backgroundColor = new Color(0.07f, 0.09f, 0.12f, 1f);
            Root.style.alignItems = Align.Center;
            Root.style.justifyContent = Justify.Center;
            Root.pickingMode = PickingMode.Position;
            // Картина режима из Scenario (если нет — ключевой арт меню): фоном, медленно
            // приближается (эффект Кена Бёрнса). Сверху — лёгкое затемнение и тёмная плашка под текстом.
            var art = (artPath != null ? GeneratedArt.Load(artPath) : null) ?? GeneratedArt.Load(GeneratedArt.KeyArt);
            if (art != null)
            {
                _art = new VisualElement();
                Ui.Fill(_art);
                _art.style.backgroundImage = art;
                _art.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                _art.pickingMode = PickingMode.Ignore;
                Root.Add(_art);
                var shade = new VisualElement();
                Ui.Fill(shade);
                shade.style.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 0.38f);
                shade.pickingMode = PickingMode.Ignore;
                Root.Add(shade);
            }

            var box = Ui.Column();
            box.style.alignItems = Align.Center;
            box.style.width = 640f;
            box.style.backgroundColor = new Color(0.05f, 0.06f, 0.09f, art != null ? 0.72f : 0f);
            Ui.Radius(box, 18f);
            box.style.paddingTop = 28f;
            box.style.paddingBottom = 26f;
            box.style.paddingLeft = 32f;
            box.style.paddingRight = 32f;
            Root.Add(box);

            _title = Ui.Text(title, 34f, Theme.Text, true);
            _title.style.marginBottom = 6f;
            box.Add(_title);
            _subtitle = Ui.Text(subtitle ?? "", 16f, Theme.TextDim);
            _subtitle.style.marginBottom = 34f;
            box.Add(_subtitle);

            // Фура едет по полосе загрузки: стоит на её конце и движется вместе с ним.
            var lane = new VisualElement();
            lane.style.width = 520f;
            lane.style.height = 22f;
            box.Add(lane);
            _truck = new TruckIcon(_accent);
            _truck.style.position = Position.Absolute;
            _truck.style.bottom = 0f;
            _truck.style.left = Length.Percent(2f);
            _truck.style.translate = new Translate(Length.Percent(-100f), new Length(0f));
            Ui.Transitions(_truck, 0.25f, "left");
            lane.Add(_truck);

            var track = new VisualElement();
            track.style.width = 520f;
            track.style.height = 10f;
            track.style.backgroundColor = new Color(1f, 1f, 1f, 0.08f);
            Ui.Radius(track, 5f);
            track.style.overflow = Overflow.Hidden;
            box.Add(track);
            _bar = new VisualElement();
            _bar.style.height = Length.Percent(100f);
            _bar.style.width = Length.Percent(2f);
            _bar.style.backgroundColor = _accent;
            Ui.Radius(_bar, 5f);
            Ui.Transitions(_bar, 0.25f, "width");
            track.Add(_bar);

            _stage = Ui.Text("", 15f, Theme.Text);
            _stage.style.marginTop = 14f;
            box.Add(_stage);

            _tipIndex = UnityEngine.Random.Range(0, _tips.Length);
            _tip = Ui.Text((LocalizationService.Language == "en" ? "Tip: " : "Совет: ") + _tips[_tipIndex], 14f, Theme.TextDim);
            _tip.style.marginTop = 34f;
            _tip.style.whiteSpace = WhiteSpace.Normal;
            _tip.style.unityTextAlign = TextAnchor.MiddleCenter;
            _tip.style.maxWidth = 560f;
            _tip.style.minHeight = 38f;
            Ui.Transitions(_tip, 0.35f, "opacity");
            box.Add(_tip);

            // Анимация: фон медленно приближается, советы сменяются каждые 5 с (с затуханием), фура покачивается.
            Root.schedule.Execute(Animate).Every(33);
        }

        void Animate(TimerState timer)
        {
            float dt = Mathf.Min(0.1f, timer.deltaTime / 1000f);
            _time += dt;
            if (_art != null && !Ui.ReducedMotion)
            {
                float zoom = 1f + 0.08f * Mathf.Clamp01(_time / 25f);
                _art.style.scale = new Scale(new Vector3(zoom, zoom, 1f));
            }
            if (_truck != null && !Ui.ReducedMotion)
                _truck.style.translate = new Translate(Length.Percent(-100f), new Length(Mathf.Sin(_time * 18f) * 0.6f));
            int slot = (int)(_time / 5f);
            if (slot != _shownSlot)
            {
                _shownSlot = slot;
                if (slot == 0) return;
                _tip.style.opacity = 0f;
                _tip.schedule.Execute(() =>
                {
                    _tipIndex = (_tipIndex + 1) % _tips.Length;
                    _tip.text = "Совет: " + _tips[_tipIndex];
                    _tip.style.opacity = 1f;
                }).ExecuteLater(350);
            }
        }

        int _shownSlot;

        /// <summary>Город сгенерирован или карта загружена — показать его название под заголовком.</summary>
        public void SetCity(string name)
        {
            if (!string.IsNullOrEmpty(name)) _subtitle.text = LocalizationService.Language == "en" ? $"City: {MagnateLocationText.Name(name)}" : $"Город «{name}»";
        }

        /// <summary>Своя подпись под заголовком (например, «Карта «Москва» · 4 компании»).</summary>
        public void SetSubtitle(string text)
        {
            if (!string.IsNullOrEmpty(text)) _subtitle.text = text;
        }

        /// <summary>Маленькая фура цвета режима: прицеп, кабина с окном, колёса.</summary>
        sealed class TruckIcon : VisualElement
        {
            readonly Color _color;

            public TruckIcon(Color color)
            {
                _color = color;
                style.width = 40f;
                style.height = 20f;
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            void Draw(MeshGenerationContext context)
            {
                var p = context.painter2D;
                void Fill(float x, float y, float w, float h, Color color)
                {
                    p.fillColor = color;
                    p.BeginPath();
                    p.MoveTo(new Vector2(x, y));
                    p.LineTo(new Vector2(x + w, y));
                    p.LineTo(new Vector2(x + w, y + h));
                    p.LineTo(new Vector2(x, y + h));
                    p.ClosePath();
                    p.Fill();
                }
                Fill(1f, 2f, 25f, 12f, _color);                       // прицеп
                Fill(27f, 6f, 11f, 8f, new Color(_color.r * 0.85f, _color.g * 0.85f, _color.b * 0.85f, 1f));              // кабина
                Fill(31f, 7.5f, 5f, 3.5f, new Color(0.75f, 0.88f, 1f, 1f));     // окно
                p.fillColor = new Color(0.12f, 0.12f, 0.14f, 1f);
                foreach (float x in new[] { 7f, 19f, 33f })
                {
                    p.BeginPath();
                    p.Arc(new Vector2(x, 16f), 3.2f, Angle.Degrees(0f), Angle.Degrees(360f));
                    p.Fill();
                }
            }
        }

        /// <summary>Выполнить шаги по одному за кадр; в конце — done. onError — сообщение об ошибке.</summary>
        public void Run(CityHighwaysApp app, IList<(string label, Action action)> steps, Action done, Action onError)
        {
            int index = 0;
            void Next()
            {
                if (index >= steps.Count)
                {
                    _bar.style.width = Length.Percent(100f);
                    _truck.style.left = Length.Percent(100f);
                    _stage.text = LocalizationService.Language == "en" ? "Ready!" : "Готово!";
                    app.Tweener.After(0.25f, done);
                    return;
                }
                var step = steps[index];
                _stage.text = step.label;
                float percent = Mathf.Max(2f, 100f * index / steps.Count);
                _bar.style.width = Length.Percent(percent);
                _truck.style.left = Length.Percent(percent);
                // Даём кадру отрисовать подпись, потом выполняем тяжёлый шаг.
                app.Tweener.After(0.06f, () =>
                {
                    try
                    {
                        step.action();
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                        Fail(app, _failText + e.Message, onError);
                        return;
                    }
                    index++;
                    Next();
                });
            }
            Next();
        }

        void Fail(CityHighwaysApp app, string message, Action onError)
        {
            _stage.text = message;
            _stage.style.color = Theme.Danger;
            var back = Ui.Button(LocalizationService.Language == "en" ? "Back to Menu" : "В меню", () => onError?.Invoke(), ButtonKind.Secondary, 15f);
            back.style.marginTop = 18f;
            _stage.parent.Add(back);
        }
    }

    /// <summary>Display-only transliteration; generated city/location IDs and saved data stay unchanged.</summary>
    internal static class MagnateLocationText
    {
        static readonly string[] Latin =
        {
            "a", "b", "v", "g", "d", "e", "yo", "zh", "z", "i", "y",
            "k", "l", "m", "n", "o", "p", "r", "s", "t", "u", "f",
            "kh", "ts", "ch", "sh", "shch", "", "y", "", "e", "yu", "ya"
        };

        public static string Name(string value)
        {
            if (LocalizationService.Language != "en" || string.IsNullOrEmpty(value)) return value;
            // Translate recurring generated station names while keeping arbitrary proper names readable.
            string[] from = { "Аэропорт", "Главный вокзал", "Северный вокзал", "Речной порт", "Вокзал", "Товарная", "Заводская", "Лесная", "Вишнёвая", "Депо", "Запад", "Восток", "Север", "Юг", "Центр" };
            string[] to = { "Airport", "Central Station", "North Station", "River Port", "Station", "Freight Station", "Factory Station", "Forest Station", "Cherry Station", "Depot", "West", "East", "North", "South", "Centre" };
            // Replace with non-Cyrillic placeholders so the transliterator doesn't touch translations.
            for (int i = 0; i < from.Length; i++)
            {
                string marker = "~" + i + "~";
                if (value.Contains(from[i]))
                {
                    value = value.Replace(from[i], marker);
                }
            }
            const string alphabet = "абвгдеёжзийклмнопрстуфхцчшщъыьэюя";
            var result = new System.Text.StringBuilder();
            foreach (char letter in value)
            {
                int index = alphabet.IndexOf(char.ToLowerInvariant(letter));
                if (index < 0) { result.Append(letter); continue; }
                string latin = Latin[index];
                if (char.IsUpper(letter) && latin.Length > 0)
                    result.Append(char.ToUpperInvariant(latin[0])).Append(latin.Substring(1));
                else result.Append(latin);
            }
            string output = result.ToString();
            for (int i = 0; i < from.Length; i++)
                output = output.Replace("~" + i + "~", to[i]);
            return output;
        }
    }
}
