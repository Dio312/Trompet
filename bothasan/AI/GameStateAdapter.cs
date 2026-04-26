// Hasan'ın GameState'ini MCTS'in anlayacağı basit formata çeviren köprü sınıfı.
// Aynı zamanda MCTS kararını Hasan'ın BotDecision formatına geri çevirir.

using System.Collections.Generic;
using System.Linq;

public static class GameStateAdapter
{
    // Hasan'ın GameState → SimplifiedGameState
    public static SimplifiedGameState ToSimplifiedState(GameState hasanState, int currentPlayerId)
    {
        var state = new SimplifiedGameState
        {
            currentPlayerId = currentPlayerId,
            players         = new List<SimplifiedPlayerState>()
        };

        foreach (var player in hasanState.Players)
        {
            var simpleFronts = BuildFrontStates(player);

            bool hasCommanderInHand = player.Hand.Any(c =>
                c.Type == CardType.Commander || c.IsJoker);

            // Joker komutan cephesine de ordu eklenebilir (sadece zayıf saldırı yapamaz)
            bool canPlaceArmy = simpleFronts.Any(f =>
                f.commanderPresent &&
                f.armyValue < 21);

            var simplePlayer = new SimplifiedPlayerState
            {
                playerId           = player.PlayerId,
                handCount          = player.Hand.Count,
                frontCount         = player.Cepheler.Count,
                totalUnits         = simpleFronts.Sum(f => f.armyValue),
                bestFrontUnits     = simpleFronts.Count > 0
                    ? simpleFronts.Max(f => f.armyValue)
                    : 0,
                hasCommanderInHand = hasCommanderInHand,
                hasOpenFront       = simpleFronts.Any(f => f.commanderPresent),
                canPlaceArmy       = canPlaceArmy,
                isUnderThreat      = false,   // doldurulacak (rakip tarama sonrası)
                fronts             = simpleFronts
            };
            state.players.Add(simplePlayer);
        }

        // Rakip tehdit hesaplama: herhangi bir rakibin bestFrontUnits >= 18 ise tehdit var
        foreach (var sp in state.players)
        {
            bool underThreat = state.players.Any(opp =>
                opp.playerId != sp.playerId && opp.bestFrontUnits >= 18);
            sp.isUnderThreat = underThreat;
        }

        state.leftDeckCount  = hasanState.LeftDeck.Count;
        state.rightDeckCount = hasanState.RightDeck.Count;
        state.exileCount     = hasanState.TrompetPile.Count;

        return state;
    }

    // Her cephe için SimplifiedFrontState listesi oluştur
    private static List<SimplifiedFrontState> BuildFrontStates(PlayerState player)
    {
        var result = new List<SimplifiedFrontState>();

        foreach (var cephe in player.Cepheler)
        {
            bool hasCommander = cephe.Commander != null;
            bool jokerCmdr    = hasCommander && cephe.Commander.IsJoker;

            result.Add(new SimplifiedFrontState
            {
                commanderPresent = hasCommander,
                isJokerCommander = jokerCmdr,
                armyValue        = cephe.TotalArmyValueForCombat(),
                armyCount        = cephe.Army.Count,
                hasArmyCards     = cephe.Army.Count > 0
            });
        }

        return result;
    }

    // SimplifiedMove → BotDecision
    public static BotDecision ToBotDecision(SimplifiedMove move, GameState hasanState, PlayerState player)
    {
        switch (move.type)
        {
            case SimplifiedMoveType.DrawLeft:
                return new BotDecision { Action = BotActionType.DrawFromLeft };

            case SimplifiedMoveType.DrawRight:
                return new BotDecision { Action = BotActionType.DrawFromRight };

            case SimplifiedMoveType.DrawSurgun:
                return new BotDecision { Action = BotActionType.DrawFromSurgun };

            case SimplifiedMoveType.PlaceCommander:
            {
                Card commander = player.Hand
                    .Where(c => c.Type == CardType.Commander || c.IsJoker)
                    .OrderBy(c => c.IsJoker ? 1 : 0)
                    .FirstOrDefault();

                if (commander == null)
                    return SafeDrawDecision(hasanState);

                return new BotDecision { Action = BotActionType.PlaceCommander, CardToPlay = commander };
            }

            case SimplifiedMoveType.PlaceArmy:
            {
                if (move.targetFrontIndex < 0 || move.targetFrontIndex >= player.Cepheler.Count)
                    return SafeDrawDecision(hasanState);

                var cephe = player.Cepheler[move.targetFrontIndex];
                if (cephe.Commander == null)
                    return SafeDrawDecision(hasanState);

                int lastVal  = cephe.Army.Count > 0 ? cephe.Army.Last().ArmyValue : 0;
                int curTotal = cephe.TotalArmyValueForAdd();

                Card armyCard = player.Hand
                    .Where(c =>
                        (c.Type == CardType.Army &&
                         c.Color == cephe.Commander.Color &&
                         c.ArmyValue >= lastVal &&
                         curTotal + c.ArmyValue <= 21)
                        ||
                        (c.IsJoker &&
                         c.Color == cephe.Commander.Color &&
                         curTotal < 21 &&
                         (21 - curTotal) >= lastVal))
                    .OrderByDescending(c => c.ArmyValue)
                    .FirstOrDefault();

                if (armyCard == null)
                    return SafeDrawDecision(hasanState);

                return new BotDecision
                {
                    Action      = BotActionType.PlaceArmy,
                    CardToPlay  = armyCard,
                    SourceCephe = cephe
                };
            }

            case SimplifiedMoveType.Attack:
            {
                if (move.sourceFrontIndex < 0 || move.sourceFrontIndex >= player.Cepheler.Count)
                    return SafeDrawDecision(hasanState);

                var srcCephe = player.Cepheler[move.sourceFrontIndex];

                // Joker komutan zayıf saldırı yapamaz
                if (srcCephe.Commander == null || srcCephe.Commander.IsJoker)
                    return SafeDrawDecision(hasanState);

                if (move.targetPlayerId < 0 || move.targetPlayerId >= hasanState.Players.Count)
                    return SafeDrawDecision(hasanState);

                var targetPlayer = hasanState.Players[move.targetPlayerId];

                if (move.targetFrontIndex < 0 || move.targetFrontIndex >= targetPlayer.Cepheler.Count)
                    return SafeDrawDecision(hasanState);

                var tgtCephe = targetPlayer.Cepheler[move.targetFrontIndex];

                // Joker komutanın cephesine zayıf saldırı yapılamaz
                if (tgtCephe.Commander != null && tgtCephe.Commander.IsJoker)
                    return SafeDrawDecision(hasanState);

                // Saldırı zinciri doğrulaması: A→J, J→K, K→Q, Q→A
                string expectedTarget = srcCephe.Commander.CommanderName switch
                {
                    "A" => "J",
                    "J" => "K",
                    "K" => "Q",
                    "Q" => "A",
                    _   => ""
                };

                if (expectedTarget == "" ||
                    tgtCephe.Commander == null ||
                    tgtCephe.Commander.CommanderName != expectedTarget)
                    return SafeDrawDecision(hasanState);

                return new BotDecision
                {
                    Action      = BotActionType.Attack,
                    SourceCephe = srcCephe,
                    TargetCephe = tgtCephe
                };
            }
        }

        return SafeDrawDecision(hasanState);
    }

    // Güvenli kart çekme fallback
    private static BotDecision SafeDrawDecision(GameState state)
    {
        if (state.LeftDeck.Count  > 0) return new BotDecision { Action = BotActionType.DrawFromLeft };
        if (state.RightDeck.Count > 0) return new BotDecision { Action = BotActionType.DrawFromRight };
        if (state.TrompetPile.Count > 0) return new BotDecision { Action = BotActionType.DrawFromSurgun };
        return new BotDecision { Action = BotActionType.DrawFromLeft };
    }
}

// ============================================================
//  VERİ YAPILARI
// ============================================================

public class SimplifiedGameState
{
    public int currentPlayerId;
    public List<SimplifiedPlayerState> players;
    public int leftDeckCount;
    public int rightDeckCount;
    public int exileCount;
}

public class SimplifiedPlayerState
{
    public int  playerId;
    public int  handCount;
    public int  frontCount;
    public int  totalUnits;        // Tüm cephelerin birlik toplamı
    public int  bestFrontUnits;    // En gelişmiş cephenin birlik değeri
    public bool hasCommanderInHand;
    public bool hasOpenFront;
    public bool canPlaceArmy;
    public bool isUnderThreat;     // Herhangi bir rakip cephesi >= 18 birliğe ulaştı mı?
    public List<SimplifiedFrontState> fronts = new List<SimplifiedFrontState>();
}

// Tek bir cephenin özet bilgisi
public class SimplifiedFrontState
{
    public bool commanderPresent;  // Komutan var mı?
    public bool isJokerCommander;  // Komutan joker mi? (zayıf saldırı yapamaz/yiyemez)
    public int  armyValue;         // Toplam savaş birliği değeri (joker max ile)
    public int  armyCount;         // Ordudaki kart sayısı
    public bool hasArmyCards;      // En az bir ordu kartı var mı?
}

public enum SimplifiedMoveType
{
    DrawLeft,
    DrawRight,
    DrawSurgun,
    PlaceCommander,
    PlaceArmy,
    Attack
}

public class SimplifiedMove
{
    public SimplifiedMoveType type;
    public int sourceFrontIndex = -1;
    public int targetPlayerId   = -1;
    public int targetFrontIndex = -1;
}
