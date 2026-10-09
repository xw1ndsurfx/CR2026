using Intersect.Framework.Core.GameObjects.NPCs;
using NUnit.Framework;

namespace Intersect.Tests.Server.Entities;

[TestFixture]
public class PetSpellRequiredLevelsTests
{
    private static NPCDescriptor CreatePet(int count = 3, int interval = 5)
    {
        var pet = new NPCDescriptor(Guid.NewGuid())
        {
            IsPet = true,
            PetSpellUnlockInterval = interval,
        };

        for (var i = 0; i < count; i++)
        {
            pet.Spells.Add(Guid.NewGuid());
        }

        return pet;
    }

    [Test]
    public void ExistingPetsKeepOriginalUnlockIntervalWithoutOverrides()
    {
        var pet = CreatePet();

        Assert.Multiple(() =>
        {
            Assert.That(pet.GetPetSpellRequiredLevel(0), Is.EqualTo(1));
            Assert.That(pet.GetPetSpellRequiredLevel(1), Is.EqualTo(6));
            Assert.That(pet.GetPetSpellRequiredLevel(2), Is.EqualTo(11));
            Assert.That(pet.HasCustomPetSpellLevel(1), Is.False);
        });
    }

    [Test]
    public void IndividualPetSpellsCanUnlockOutOfOrder()
    {
        var pet = CreatePet();
        pet.SetPetSpellRequiredLevel(1, 25);
        pet.SetPetSpellRequiredLevel(2, 3);

        Assert.Multiple(() =>
        {
            Assert.That(pet.GetPetSpellRequiredLevel(0), Is.EqualTo(1));
            Assert.That(pet.GetPetSpellRequiredLevel(1), Is.EqualTo(25));
            Assert.That(pet.GetPetSpellRequiredLevel(2), Is.EqualTo(3));
            Assert.That(pet.HasCustomPetSpellLevel(0), Is.False);
            Assert.That(pet.HasCustomPetSpellLevel(1), Is.True);
        });
    }

    [Test]
    public void IdenticalSpellIdsMayHaveDifferentSlotLevels()
    {
        var pet = CreatePet(2);
        pet.Spells[1] = pet.Spells[0];
        pet.SetPetSpellRequiredLevel(0, 4);
        pet.SetPetSpellRequiredLevel(1, 22);

        Assert.Multiple(() =>
        {
            Assert.That(pet.GetPetSpellRequiredLevel(0), Is.EqualTo(4));
            Assert.That(pet.GetPetSpellRequiredLevel(1), Is.EqualTo(22));
        });
    }

    [Test]
    public void ResetRestoresTheDynamicLegacyInterval()
    {
        var pet = CreatePet(2);
        pet.SetPetSpellRequiredLevel(1, 30);
        pet.ResetPetSpellRequiredLevel(1);
        pet.PetSpellUnlockInterval = 10;

        Assert.Multiple(() =>
        {
            Assert.That(pet.GetPetSpellRequiredLevel(1), Is.EqualTo(11));
            Assert.That(pet.HasCustomPetSpellLevel(1), Is.False);
        });
    }

    [Test]
    public void SlotSettingsPersistAsJsonIncludingAutomaticEntries()
    {
        var pet = CreatePet();
        pet.SetPetSpellRequiredLevel(1, 17);
        var json = pet.PetSpellRequiredLevelsJson;

        var loaded = CreatePet();
        loaded.PetSpellRequiredLevelsJson = json;

        Assert.Multiple(() =>
        {
            Assert.That(loaded.GetPetSpellRequiredLevel(0), Is.EqualTo(1));
            Assert.That(loaded.GetPetSpellRequiredLevel(1), Is.EqualTo(17));
            Assert.That(loaded.GetPetSpellRequiredLevel(2), Is.EqualTo(11));
        });
    }

    [Test]
    public void EmptyPriorDatabaseValueIsHandledWithoutDroppingSpells()
    {
        var pet = CreatePet();
        pet.PetSpellRequiredLevelsJson = null!;
        pet.EnsurePetSpellLevelSlots();

        Assert.Multiple(() =>
        {
            Assert.That(pet.Spells.Count, Is.EqualTo(3));
            Assert.That(pet.PetSpellRequiredLevels.Count, Is.EqualTo(3));
            Assert.That(pet.GetPetSpellRequiredLevel(2), Is.EqualTo(11));
        });
    }
}
