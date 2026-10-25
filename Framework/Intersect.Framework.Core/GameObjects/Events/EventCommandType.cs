namespace Intersect.Framework.Core.GameObjects.Events;

public enum EventCommandType
{
    Null = 0,

    //Dialog
    ShowText,

    ShowOptions,

    AddChatboxText,

    //Logic Flow
    SetVariable = 5,

    SetSelfSwitch,

    ConditionalBranch,

    ExitEventProcess,

    Label,

    GoToLabel,

    StartCommonEvent,

    //Player Control
    RestoreHp,

    RestoreMp,

    LevelUp,

    GiveExperience,

    ChangeLevel,

    ChangeSpells,

    ChangeItems,

    ChangeSprite,

    ChangeFace,

    ChangeGender,

    SetAccess,

    //Movement,
    WarpPlayer,

    SetMoveRoute,

    WaitForRouteCompletion,

    HoldPlayer,

    ReleasePlayer,

    SpawnNpc,

    //Special Effects
    PlayAnimation,

    PlayBgm,

    FadeoutBgm,

    PlaySound,

    StopSounds,

    //Etc
    Wait,

    //Shop and Bank
    OpenBank,

    OpenShop,

    OpenCraftingTable,

    //Extras
    SetClass,

    DespawnNpc,

    //Questing
    StartQuest,

    CompleteQuestTask,

    EndQuest,

    //Pictures
    ShowPicture,

    HidePicture,

    //Hide/show player
    HidePlayer,

    ShowPlayer,

    //Equip Items
    EquipItem,

    //Change Name Color
    ChangeNameColor,

    //Player Input variable.
    InputVariable,

    //Player Label
    PlayerLabel,

    // Player Color
    ChangePlayerColor,

    ChangeName,

    //Guilds
    CreateGuild,
    DisbandGuild,
    OpenGuildBank,
    SetGuildBankSlots,

    //End Guilds

    //Reset Stats
    ResetStatPointAllocations,

    CastSpellOn,

    Fade,

    // CR2026 mini-games: reserved range; never renumber existing serialized commands.
    StartMiniGame = 1000,
    LeaveMiniGame = 1001,
    OpenLogiklikNews = 1002,
    OpenMarketplace = 1003,

    // CR2026 professions: explicit value preserves serialized command ids.
    ModifyProfession = 1010,

    // CR2026 dungeons.
    StartDungeon = 1020,

    // CR2026 quest shops.
    OpenQuestShop = 1030,

    // CR2026 premium character appearance shops.
    OpenRoyalStylist = 1040,

    OpenNavalShop = 1050,
    OpenNavalAmmoShop = 1051,
    GrantExperienceBonus = 1060,
    GrantLootBonus = 1061,
    OpenAdventureLobby = 1062,
    ForceRain = 1063,
    BeginCinematic = 1070,
    CinematicCamera = 1071,
    PlayCinematic = 1072,
    StopCinematic = 1073,
    WaitForCinematic = 1074,
    StartCinematicChapter = 1075,
    CinematicActor = 1076,
    AwaitCinematicCondition = 1077,
    CinematicQuestTask = 1078,
    CinematicEnvironment = 1079,
}
