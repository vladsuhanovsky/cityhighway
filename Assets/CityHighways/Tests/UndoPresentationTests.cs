using CityHighways.Core;
using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class UndoPresentationTests
    {
        [Test]
        public void EveryReversibleCommandHasActionSpecificRussianAndEnglishDescription()
        {
            var cases = new[]
            {
                GameCommand.DrawCardFromDeck(0),
                GameCommand.DrawFaceUpCard(0, 1),
                GameCommand.BuildRoad(0, "r1", Resource.Asphalt, 0),
                GameCommand.BuildRoadFree(0, "r1"),
                GameCommand.UpgradeRoad(0, "r1", Resource.Asphalt, 1)
            };

            foreach (var command in cases)
            {
                Assert.IsTrue(GameUndo.Reversible(command));
                string en = UndoPresentation.ActionLabel(command, true);
                string ru = UndoPresentation.ActionLabel(command, false);
                Assert.IsNotEmpty(en);
                Assert.IsNotEmpty(ru);
                Assert.AreNotEqual("last action", en);
                Assert.AreNotEqual("последнее действие", ru);
                Assert.AreNotEqual(en, ru);
            }
        }

        [Test]
        public void UnknownAndNullCommandsUseHonestGenericLabel()
        {
            Assert.AreEqual("last action",
                UndoPresentation.ActionLabel(null, true));
            Assert.AreEqual("последнее действие",
                UndoPresentation.ActionLabel(new GameCommand { kind = "unknown" }, false));
        }

        [Test]
        public void PreviousPlayerUndoMustBeConfirmed()
        {
            var lastMove = GameCommand.DrawCardFromDeck(0);
            Assert.IsFalse(UndoPresentation.RequiresConfirmation(lastMove, 0));
            Assert.IsTrue(UndoPresentation.RequiresConfirmation(lastMove, 1));
            Assert.IsFalse(UndoPresentation.RequiresConfirmation(lastMove, -1));
            Assert.IsFalse(UndoPresentation.RequiresConfirmation(null, 1));
        }

        [Test]
        public void ConfirmationIsBoundToCommandOwnerHistoryAndDeadline()
        {
            Assert.IsTrue(UndoPresentation.ConfirmationValid(5, 5, 0, 0, 10f, 15f));
            Assert.IsTrue(UndoPresentation.ConfirmationValid(5, 5, 0, 0, 15f, 15f));
            Assert.IsFalse(UndoPresentation.ConfirmationValid(6, 5, 0, 0, 10f, 15f));
            Assert.IsFalse(UndoPresentation.ConfirmationValid(5, 5, 1, 0, 10f, 15f));
            Assert.IsFalse(UndoPresentation.ConfirmationValid(5, 5, 0, 0, 15.1f, 15f));
            Assert.IsFalse(UndoPresentation.ConfirmationValid(0, 0, 0, 0, 10f, 15f));
        }

        [Test]
        public void ShortcutCannotFireWithoutModifierOrOutsideSafeState()
        {
            Assert.IsTrue(UndoPresentation.WantsShortcut(true, false, true, false, true));
            Assert.IsFalse(UndoPresentation.WantsShortcut(false, false, true, false, true));
            Assert.IsFalse(UndoPresentation.WantsShortcut(true, false, false, false, true));
            Assert.IsFalse(UndoPresentation.WantsShortcut(true, true, true, false, true));
            Assert.IsFalse(UndoPresentation.WantsShortcut(true, false, true, true, true));
            Assert.IsFalse(UndoPresentation.WantsShortcut(true, false, true, false, false));
        }
    }
}
