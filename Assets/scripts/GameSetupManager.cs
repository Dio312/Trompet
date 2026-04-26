// GameSetupManager.cs
// Complete debug rule engine + F1-F12 scenario testing system.
// Phase 5: Debug Scenario Tools added.

using System.Collections.Generic;
using UnityEngine;

public class GameSetupManager : MonoBehaviour
{
    // -------------------------------------------------------
    // GAME STATE
    // -------------------------------------------------------

    List<PlayerState> Players = new List<PlayerState>();
    List<Card> LeftDrawPile = new List<Card>();
    List<Card> RightDrawPile = new List<Card>();
    List<Card> ExilePile = new List<Card>();
    List<Card> DestroyedCards = new List<Card>();
    Card TrumpetCard;
    bool TrumpetInCenter = true;
    bool gameOver = false;

    // -------------------------------------------------------
    // TURN STATE
    // -------------------------------------------------------

    int currentPlayerIndex = 0;
    int selectedCardIndex = -1;
    int selectedOwnFrontlineIndex = -1;
    int targetPlayerIndex = -1;
    int targetFrontlineIndex = -1;

    // -------------------------------------------------------
    // SUPER ATTACK STATE
    // -------------------------------------------------------

    bool mustResolveSuperAttack = false;
    int superAttackOwnerPlayerIndex = -1;
    int superAttackOwnerFrontlineIndex = -1;

    // -------------------------------------------------------
    // FORCED DRAW STATE
    // -------------------------------------------------------

    bool isForcedDrawActive = false;
    int forcedDrawPlayerIndex = -1;
    int forcedDrawRemaining = 0;
    bool afterForcedDrawReturnToSamePlayer = false;

    // -------------------------------------------------------
    // START
    // -------------------------------------------------------

    void Start()
    {
        SetupNormalGame();
    }

    // -------------------------------------------------------
    // UPDATE
    // -------------------------------------------------------

    void Update()
    {
        // F-key scenario shortcuts — always available
        if (Input.GetKeyDown(KeyCode.F1))  ScenarioNormalGame();
        if (Input.GetKeyDown(KeyCode.F2))  ScenarioBasicFrontlineTest();
        if (Input.GetKeyDown(KeyCode.F3))  ScenarioValidWeakAttack();
        if (Input.GetKeyDown(KeyCode.F4))  ScenarioInvalidWeakAttack();
        if (Input.GetKeyDown(KeyCode.F5))  ScenarioJokerCommanderBlock();
        if (Input.GetKeyDown(KeyCode.F6))  ScenarioMandatorySuperAttack();
        if (Input.GetKeyDown(KeyCode.F7))  ScenarioJokerArmyDynamicValue();
        if (Input.GetKeyDown(KeyCode.F8))  ScenarioExileTest();
        if (Input.GetKeyDown(KeyCode.F9))  ScenarioForcedDrawTest();
        if (Input.GetKeyDown(KeyCode.F10)) ScenarioTrumpetWin();
        if (Input.GetKeyDown(KeyCode.F11)) ScenarioEmptyHandWin();
        if (Input.GetKeyDown(KeyCode.F12)) PrintScenarioHelp();

        // Always available
        if (Input.GetKeyDown(KeyCode.P)) PrintFullGameState();
        if (Input.GetKeyDown(KeyCode.C)) PrintControls();
        if (Input.GetKeyDown(KeyCode.B)) PrintTargetSelection();

        if (gameOver)
        {
            return; // Block everything except F-keys and print keys above
        }

        // Forced draw mode
        if (isForcedDrawActive)
        {
            HandleForcedDrawInput();
            return;
        }

        // Mandatory Super Attack mode
        if (mustResolveSuperAttack)
        {
            HandleTargetSelectionInput();
            if (Input.GetKeyDown(KeyCode.S)) TryResolveMandatorySuperAttack();
            return;
        }

        // Normal turn input
        PlayerState current = Players[currentPlayerIndex];

        if (Input.GetKeyDown(KeyCode.H))
        {
            Debug.Log($"=== Player {current.PlayerIndex}'s Hand ===");
            current.PrintHandWithIndexes();
        }

        if (Input.GetKeyDown(KeyCode.F) && !Input.GetKeyDown(KeyCode.F1)
            && !Input.GetKeyDown(KeyCode.F2) && !Input.GetKeyDown(KeyCode.F3))
        {
            // Guard: only print frontlines if no F-key scenario is also being pressed.
            // Unity reads both in the same frame sometimes on some systems.
            Debug.Log($"=== Player {current.PlayerIndex}'s Frontlines ===");
            current.PrintFrontlines();
        }

        if (Input.GetKeyDown(KeyCode.N))
        {
            Debug.Log("[DEBUG ONLY] Skipping turn.");
            AdvanceTurn();
        }

        // Hand card selection (0-9)
        for (int i = 0; i <= 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha0 + i))
            {
                if (i < current.Hand.Count)
                {
                    selectedCardIndex = i;
                    Debug.Log($"Selected hand card [{selectedCardIndex}]: {current.Hand[selectedCardIndex]}");
                }
                else
                {
                    Debug.LogWarning($"No card at index {i}. Hand has {current.Hand.Count} card(s).");
                }
            }
        }

        // Own frontline selection
        if (Input.GetKeyDown(KeyCode.Q)) { selectedOwnFrontlineIndex = 0; Debug.Log("Own frontline selected: 0"); }
        if (Input.GetKeyDown(KeyCode.W)) { selectedOwnFrontlineIndex = 1; Debug.Log("Own frontline selected: 1"); }
        if (Input.GetKeyDown(KeyCode.E)) { selectedOwnFrontlineIndex = 2; Debug.Log("Own frontline selected: 2"); }

        // Target selection
        HandleTargetSelectionInput();

        // Moves
        if (Input.GetKeyDown(KeyCode.L)) TryDrawFromPile(current, LeftDrawPile, "Left Draw Pile", true);
        if (Input.GetKeyDown(KeyCode.R)) TryDrawFromPile(current, RightDrawPile, "Right Draw Pile", true);
        if (Input.GetKeyDown(KeyCode.X)) TryDrawFromExile(current);
        if (Input.GetKeyDown(KeyCode.T)) TryDrawTrumpet(current);
        if (Input.GetKeyDown(KeyCode.O)) TryOpenFrontline(current);
        if (Input.GetKeyDown(KeyCode.A)) TryAddArmyCard(current);
        if (Input.GetKeyDown(KeyCode.Z)) TryExileTopCard(current);
        if (Input.GetKeyDown(KeyCode.V)) TryWeakAttack(current);
        if (Input.GetKeyDown(KeyCode.S)) TryManualSuperAttack(current);
    }

    // -------------------------------------------------------
    // TARGET SELECTION
    // -------------------------------------------------------

    void HandleTargetSelectionInput()
    {
        if (Input.GetKeyDown(KeyCode.Y)) CycleTargetPlayer();
        if (Input.GetKeyDown(KeyCode.U)) CycleTargetFrontline();
    }

    void CycleTargetPlayer()
    {
        int start = (targetPlayerIndex < 0) ? currentPlayerIndex : targetPlayerIndex;
        int next = (start + 1) % 4;
        int tries = 0;
        while (next == currentPlayerIndex && tries < 4) { next = (next + 1) % 4; tries++; }
        targetPlayerIndex = next;
        targetFrontlineIndex = -1;
        Debug.Log($"Target player: Player {Players[targetPlayerIndex].PlayerIndex}");
    }

    void CycleTargetFrontline()
    {
        if (targetPlayerIndex < 0) { Debug.LogWarning("Select a target player first with Y."); return; }
        PlayerState tp = Players[targetPlayerIndex];
        if (tp.Frontlines.Count == 0) { Debug.LogWarning($"Player {tp.PlayerIndex} has no frontlines."); return; }
        targetFrontlineIndex = (targetFrontlineIndex + 1) % tp.Frontlines.Count;
        Debug.Log($"Target frontline: {targetFrontlineIndex} (Commander: {tp.Frontlines[targetFrontlineIndex].Commander})");
    }

    void PrintTargetSelection()
    {
        if (targetPlayerIndex < 0) { Debug.Log("No target player selected. Press Y."); return; }
        PlayerState tp = Players[targetPlayerIndex];
        string fl = (targetFrontlineIndex >= 0 && targetFrontlineIndex < tp.Frontlines.Count)
            ? $"Frontline {targetFrontlineIndex}: {tp.Frontlines[targetFrontlineIndex].Commander}"
            : "No frontline selected (press U)";
        Debug.Log($"Target: Player {tp.PlayerIndex} | {fl}");
    }

    // -------------------------------------------------------
    // DRAW FROM PILE
    // -------------------------------------------------------

    void TryDrawFromPile(PlayerState player, List<Card> pile, string pileName, bool advanceTurn)
    {
        if (pile.Count == 0)
        {
            Debug.LogWarning($"{pileName} is empty. Player {player.PlayerIndex} could not draw.");
            return;
        }
        Card drawn = pile[pile.Count - 1];
        pile.RemoveAt(pile.Count - 1);
        player.Hand.Add(drawn);
        Debug.Log($"Player {player.PlayerIndex} drew from {pileName}: {drawn} | Hand: {player.Hand.Count} | Left: {LeftDrawPile.Count} | Right: {RightDrawPile.Count} | Exile: {ExilePile.Count}");
        RunCardCountSafetyCheck();
        if (advanceTurn) AdvanceTurn();
    }

    void TryDrawFromExile(PlayerState player)
    {
        if (ExilePile.Count == 0)
        {
            Debug.LogWarning($"Exile Pile is empty. Player {player.PlayerIndex} could not draw.");
            return;
        }
        TryDrawFromPile(player, ExilePile, "Exile Pile", true);
    }

    // -------------------------------------------------------
    // DRAW TRUMPET
    // -------------------------------------------------------

    void TryDrawTrumpet(PlayerState player)
    {
        if (!TrumpetInCenter) { Debug.LogWarning("Trumpet has already been drawn."); return; }
        if (LeftDrawPile.Count > 0 || RightDrawPile.Count > 0 || ExilePile.Count > 0)
        {
            Debug.LogWarning($"Cannot draw Trumpet yet. Left: {LeftDrawPile.Count} Right: {RightDrawPile.Count} Exile: {ExilePile.Count}");
            return;
        }
        TrumpetInCenter = false;
        player.Hand.Add(TrumpetCard);
        Debug.Log($"Player {player.PlayerIndex} drew the TRUMPET!");
        ResolveTrumpetWin();
    }

    void ResolveTrumpetWin()
    {
        Debug.Log("========== TRUMPET WIN CONDITION ==========");
        int bestScore = -1; int winnerIndex = -1; bool tie = false;
        for (int i = 0; i < Players.Count; i++)
        {
            int strength = Players[i].GetTotalArmyStrength();
            Debug.Log($"Player {Players[i].PlayerIndex} total army strength: {strength}");
            if (strength > bestScore) { bestScore = strength; winnerIndex = i; tie = false; }
            else if (strength == bestScore) { tie = true; }
        }
        if (tie) Debug.Log("RESULT: TIE! Tiebreaker rule not specified yet.");
        else Debug.Log($"WINNER: Player {Players[winnerIndex].PlayerIndex} with strength {bestScore}!");
        gameOver = true;
        Debug.Log("========== GAME OVER ==========");
    }

    // -------------------------------------------------------
    // OPEN FRONTLINE
    // -------------------------------------------------------

    void TryOpenFrontline(PlayerState player)
    {
        if (!ValidateCardSelected(player)) return;
        Card card = player.Hand[selectedCardIndex];
        string reason;
        if (!player.CanOpenFrontline(card, out reason)) { Debug.LogWarning($"Cannot open frontline: {reason}"); return; }
        player.OpenFrontline(card);
        int fi = player.Frontlines.Count - 1;
        Debug.Log($"SUCCESS: Player {player.PlayerIndex} opened Frontline {fi} with {card}");
        player.Frontlines[fi].PrintFrontline(fi);
        selectedCardIndex = -1;
        RunCardCountSafetyCheck();
        if (CheckHandEmptyWin(player)) return;
        AdvanceTurn();
    }

    // -------------------------------------------------------
    // ADD ARMY CARD
    // -------------------------------------------------------

    void TryAddArmyCard(PlayerState player)
    {
        if (!ValidateCardSelected(player)) return;
        if (!ValidateOwnFrontlineSelected(player)) return;
        Card card = player.Hand[selectedCardIndex];
        FrontlineState frontline = player.Frontlines[selectedOwnFrontlineIndex];
        string reason;
        if (!frontline.CanAddArmy(card, out reason)) { Debug.LogWarning($"Cannot add army card: {reason}"); return; }
        bool willHit21 = frontline.WouldReachExactly21AfterAdding(card);
        player.Hand.RemoveAt(selectedCardIndex);
        frontline.AddArmy(card);
        selectedCardIndex = -1;
        int effective = frontline.GetEffectiveArmyTotal();
        Debug.Log($"SUCCESS: Player {player.PlayerIndex} added {card} to Frontline {selectedOwnFrontlineIndex}. Effective total: {effective}");
        frontline.PrintFrontline(selectedOwnFrontlineIndex);
        RunCardCountSafetyCheck();
        if (willHit21)
        {
            if (!AnyOpponentFrontlinesExist(currentPlayerIndex))
            {
                Debug.Log("Frontline reached 21 but no opponent frontlines exist. Super Attack skipped.");
                if (CheckHandEmptyWin(player)) return;
                return;
            }
            mustResolveSuperAttack = true;
            superAttackOwnerPlayerIndex = currentPlayerIndex;
            superAttackOwnerFrontlineIndex = selectedOwnFrontlineIndex;
            selectedOwnFrontlineIndex = -1;
            Debug.Log($"*** MANDATORY SUPER ATTACK! Player {player.PlayerIndex} Frontline {superAttackOwnerFrontlineIndex} reached 21. ***");
            Debug.Log("Press Y → target player, U → target frontline, S → Super Attack. Normal moves BLOCKED.");
            return;
        }
        if (CheckHandEmptyWin(player)) return;
        AdvanceTurn();
    }

    // -------------------------------------------------------
    // EXILE TOP CARD
    // -------------------------------------------------------

    void TryExileTopCard(PlayerState player)
    {
        if (!ValidateOwnFrontlineSelected(player)) return;
        FrontlineState frontline = player.Frontlines[selectedOwnFrontlineIndex];
        if (frontline.ArmyCards.Count > 0)
        {
            Card exiled = frontline.ArmyCards[frontline.ArmyCards.Count - 1];
            frontline.ArmyCards.RemoveAt(frontline.ArmyCards.Count - 1);
            ExilePile.Add(exiled);
            Debug.Log($"Player {player.PlayerIndex} exiled {exiled} from Frontline {selectedOwnFrontlineIndex}. Exile pile: {ExilePile.Count}");
        }
        else
        {
            Card exiled = frontline.Commander;
            player.Frontlines.RemoveAt(selectedOwnFrontlineIndex);
            ExilePile.Add(exiled);
            Debug.Log($"Player {player.PlayerIndex} exiled commander {exiled}. Frontline destroyed. Exile pile: {ExilePile.Count}");
            selectedOwnFrontlineIndex = -1;
        }
        RunCardCountSafetyCheck();
        AdvanceTurn();
    }

    // -------------------------------------------------------
    // WEAK ATTACK
    // -------------------------------------------------------

    void TryWeakAttack(PlayerState attacker)
    {
        if (!ValidateOwnFrontlineSelected(attacker)) return;
        if (!ValidateTargetSelected()) return;
        FrontlineState af = attacker.Frontlines[selectedOwnFrontlineIndex];
        PlayerState defender = Players[targetPlayerIndex];
        if (targetFrontlineIndex < 0 || targetFrontlineIndex >= defender.Frontlines.Count)
        { Debug.LogWarning("Invalid target frontline index."); return; }
        FrontlineState df = defender.Frontlines[targetFrontlineIndex];
        if (af.IsJokerCommander) { Debug.LogWarning("Weak Attack BLOCKED: Joker commander cannot perform Weak Attack."); return; }
        if (df.IsJokerCommander) { Debug.LogWarning("Weak Attack BLOCKED: Cannot target a Joker commander frontline with Weak Attack."); return; }
        if (af.ArmyCards.Count == 0) { Debug.LogWarning("Weak Attack BLOCKED: Attacker has no army cards."); return; }
        if (!IsWeakAttackValid(af.Commander.CommanderVal, df.Commander.CommanderVal))
        { Debug.LogWarning($"Weak Attack BLOCKED: {af.Commander.CommanderVal} cannot attack {df.Commander.CommanderVal}. (A→J, J→K, K→Q, Q→A)"); return; }
        int ap = af.GetEffectiveArmyTotal();
        int dp2 = df.GetEffectiveArmyTotal();
        if (ap <= dp2) { Debug.LogWarning($"Weak Attack BLOCKED: Attacker power {ap} is not greater than defender power {dp2}."); return; }
        Debug.Log($"*** WEAK ATTACK: Player {attacker.PlayerIndex} ({af.Commander.CommanderVal}, power {ap}) → Player {defender.PlayerIndex} ({df.Commander.CommanderVal}, power {dp2}) ***");
        Card attackerLoss = af.ArmyCards[af.ArmyCards.Count - 1];
        af.ArmyCards.RemoveAt(af.ArmyCards.Count - 1);
        DestroyedCards.Add(attackerLoss);
        Debug.Log($"Attacker loses last army card: {attackerLoss} (removed from game)");
        int defenderLost = 0;
        if (df.ArmyCards.Count > 0)
        {
            defenderLost = df.ArmyCards.Count;
            foreach (Card c in df.ArmyCards) DestroyedCards.Add(c);
            df.ArmyCards.Clear();
            Debug.Log($"Defender loses {defenderLost} army card(s).");
        }
        else
        {
            defenderLost = 1;
            DestroyedCards.Add(df.Commander);
            defender.Frontlines.RemoveAt(targetFrontlineIndex);
            targetFrontlineIndex = -1;
            Debug.Log("Defender had no army cards. Commander destroyed, frontline removed.");
        }
        RunCardCountSafetyCheck();
        if (defenderLost > 0) StartForcedDraw(targetPlayerIndex, defenderLost, false);
        else AdvanceTurn();
    }

    bool IsWeakAttackValid(CommanderValue a, CommanderValue d)
    {
        if (a == CommanderValue.A && d == CommanderValue.J) return true;
        if (a == CommanderValue.J && d == CommanderValue.K) return true;
        if (a == CommanderValue.K && d == CommanderValue.Q) return true;
        if (a == CommanderValue.Q && d == CommanderValue.A) return true;
        return false;
    }

    // -------------------------------------------------------
    // SUPER ATTACK
    // -------------------------------------------------------

    void TryResolveMandatorySuperAttack()
    {
        if (!mustResolveSuperAttack) { Debug.LogWarning("No mandatory Super Attack to resolve."); return; }
        if (!ValidateTargetSelected()) return;
        PlayerState attacker = Players[superAttackOwnerPlayerIndex];
        PlayerState defender = Players[targetPlayerIndex];
        if (targetFrontlineIndex < 0 || targetFrontlineIndex >= defender.Frontlines.Count)
        { Debug.LogWarning("Invalid target frontline."); return; }
        ExecuteSuperAttack(attacker, superAttackOwnerFrontlineIndex, defender, targetFrontlineIndex, true);
        mustResolveSuperAttack = false;
        superAttackOwnerPlayerIndex = -1;
        superAttackOwnerFrontlineIndex = -1;
    }

    void TryManualSuperAttack(PlayerState player)
    {
        if (mustResolveSuperAttack) { TryResolveMandatorySuperAttack(); return; }
        if (!ValidateOwnFrontlineSelected(player)) return;
        if (!ValidateTargetSelected()) return;
        FrontlineState af = player.Frontlines[selectedOwnFrontlineIndex];
        if (af.GetEffectiveArmyTotal() != 21)
        { Debug.LogWarning($"Super Attack requires exactly 21. Current: {af.GetEffectiveArmyTotal()}"); return; }
        PlayerState defender = Players[targetPlayerIndex];
        if (targetFrontlineIndex < 0 || targetFrontlineIndex >= defender.Frontlines.Count)
        { Debug.LogWarning("Invalid target frontline."); return; }
        ExecuteSuperAttack(player, selectedOwnFrontlineIndex, defender, targetFrontlineIndex, false);
    }

    void ExecuteSuperAttack(PlayerState attacker, int afi, PlayerState defender, int dfi, bool returnToSamePlayer)
    {
        FrontlineState af = attacker.Frontlines[afi];
        FrontlineState df = defender.Frontlines[dfi];
        Debug.Log($"*** SUPER ATTACK: Player {attacker.PlayerIndex} Frontline {afi} → Player {defender.PlayerIndex} Frontline {dfi} ***");
        int defLost = 1 + df.ArmyCards.Count;
        DestroyedCards.Add(df.Commander);
        foreach (Card c in df.ArmyCards) DestroyedCards.Add(c);
        defender.Frontlines.RemoveAt(dfi);
        targetFrontlineIndex = -1;
        Debug.Log($"Defender frontline destroyed: {defLost} card(s) removed.");
        foreach (Card c in af.ArmyCards) DestroyedCards.Add(c);
        int atkArmyLost = af.ArmyCards.Count;
        af.ArmyCards.Clear();
        Debug.Log($"Attacker loses {atkArmyLost} army card(s). Commander stays.");
        RunCardCountSafetyCheck();
        if (defLost > 0) StartForcedDraw(defender.PlayerIndex - 1, defLost, returnToSamePlayer);
        else { if (!returnToSamePlayer) AdvanceTurn(); else Debug.Log($"Player {attacker.PlayerIndex} still has their normal move."); }
    }

    // -------------------------------------------------------
    // FORCED DRAW
    // -------------------------------------------------------

    void StartForcedDraw(int defenderListIndex, int amount, bool returnAfter)
    {
        isForcedDrawActive = true;
        forcedDrawPlayerIndex = defenderListIndex;
        forcedDrawRemaining = amount;
        afterForcedDrawReturnToSamePlayer = returnAfter;
        Debug.Log($"*** FORCED DRAW: Player {Players[forcedDrawPlayerIndex].PlayerIndex} must draw {forcedDrawRemaining} card(s). Press L/R/X/T. ***");
    }

    void HandleForcedDrawInput()
    {
        bool drew = false;
        PlayerState dp = Players[forcedDrawPlayerIndex];

        if (Input.GetKeyDown(KeyCode.L)) drew = TryForcedDrawFromPile(dp, LeftDrawPile, "Left Draw Pile");
        else if (Input.GetKeyDown(KeyCode.R)) drew = TryForcedDrawFromPile(dp, RightDrawPile, "Right Draw Pile");
        else if (Input.GetKeyDown(KeyCode.X)) drew = TryForcedDrawFromPile(dp, ExilePile, "Exile Pile");
        else if (Input.GetKeyDown(KeyCode.T))
        {
            if (LeftDrawPile.Count == 0 && RightDrawPile.Count == 0 && ExilePile.Count == 0 && TrumpetInCenter)
            {
                TrumpetInCenter = false;
                dp.Hand.Add(TrumpetCard);
                Debug.Log($"Player {dp.PlayerIndex} drew TRUMPET during forced draw!");
                isForcedDrawActive = false;
                RunCardCountSafetyCheck();
                ResolveTrumpetWin();
                return;
            }
            else Debug.LogWarning("Cannot draw Trumpet yet during forced draw.");
        }

        if (drew)
        {
            forcedDrawRemaining--;
            Debug.Log($"Forced draw: {forcedDrawRemaining} card(s) remaining for Player {dp.PlayerIndex}.");
            if (forcedDrawRemaining <= 0) EndForcedDraw();
        }
    }

    bool TryForcedDrawFromPile(PlayerState player, List<Card> pile, string name)
    {
        if (pile.Count == 0) { Debug.LogWarning($"{name} is empty."); return false; }
        Card drawn = pile[pile.Count - 1];
        pile.RemoveAt(pile.Count - 1);
        player.Hand.Add(drawn);
        Debug.Log($"[Forced Draw] Player {player.PlayerIndex} drew {drawn} from {name}.");
        RunCardCountSafetyCheck();
        return true;
    }

    void EndForcedDraw()
    {
        isForcedDrawActive = false;
        Debug.Log("Forced draw complete.");
        if (afterForcedDrawReturnToSamePlayer)
            Debug.Log($"Player {Players[currentPlayerIndex].PlayerIndex} still has their normal move.");
        else
            AdvanceTurn();
    }

    // -------------------------------------------------------
    // WIN CONDITIONS
    // -------------------------------------------------------

    bool CheckHandEmptyWin(PlayerState player)
    {
        if (player.Hand.Count == 0)
        {
            Debug.Log($"========== PLAYER {player.PlayerIndex} WINS BY EMPTY HAND! ==========");
            gameOver = true;
            return true;
        }
        return false;
    }

    bool AnyOpponentFrontlinesExist(int currentIdx)
    {
        for (int i = 0; i < Players.Count; i++)
            if (i != currentIdx && Players[i].Frontlines.Count > 0) return true;
        return false;
    }

    // -------------------------------------------------------
    // TURN MANAGEMENT
    // -------------------------------------------------------

    void AdvanceTurn()
    {
        currentPlayerIndex = (currentPlayerIndex + 1) % 4;
        selectedCardIndex = -1;
        selectedOwnFrontlineIndex = -1;
        Debug.Log($"--- Player {Players[currentPlayerIndex].PlayerIndex}'s turn ---");
    }

    // -------------------------------------------------------
    // VALIDATION HELPERS
    // -------------------------------------------------------

    bool ValidateCardSelected(PlayerState player)
    {
        if (selectedCardIndex < 0 || selectedCardIndex >= player.Hand.Count)
        { Debug.LogWarning("No valid card selected. Press 0–9."); return false; }
        return true;
    }

    bool ValidateOwnFrontlineSelected(PlayerState player)
    {
        if (selectedOwnFrontlineIndex < 0)
        { Debug.LogWarning("No own frontline selected. Press Q/W/E."); return false; }
        if (selectedOwnFrontlineIndex >= player.Frontlines.Count)
        { Debug.LogWarning($"Frontline {selectedOwnFrontlineIndex} doesn't exist. Player has {player.Frontlines.Count}."); return false; }
        return true;
    }

    bool ValidateTargetSelected()
    {
        if (targetPlayerIndex < 0) { Debug.LogWarning("No target player. Press Y."); return false; }
        if (targetPlayerIndex == currentPlayerIndex) { Debug.LogWarning("Cannot target yourself."); return false; }
        if (targetFrontlineIndex < 0) { Debug.LogWarning("No target frontline. Press U."); return false; }
        return true;
    }

    // -------------------------------------------------------
    // GAME SETUP (normal random game)
    // -------------------------------------------------------

    void SetupNormalGame()
    {
        List<Card> deck = BuildDeck();
        ShuffleDeck(deck);
        Players.Clear();
        for (int i = 0; i < 4; i++) Players.Add(new PlayerState(i + 1));
        foreach (PlayerState p in Players)
            for (int i = 0; i < 10; i++) { p.Hand.Add(deck[0]); deck.RemoveAt(0); }
        LeftDrawPile.Clear();
        RightDrawPile.Clear();
        for (int i = 0; i < 7; i++) { LeftDrawPile.Add(deck[0]); deck.RemoveAt(0); }
        for (int i = 0; i < 7; i++) { RightDrawPile.Add(deck[0]); deck.RemoveAt(0); }
        ExilePile.Clear();
        DestroyedCards.Clear();
        TrumpetCard = new Card(CardType.Trumpet, CardColor.None);
        TrumpetInCenter = true;
        gameOver = false;
        currentPlayerIndex = 0;
        ResetAllSelections();
        ResetAllSpecialStates();
        PrintSetupReport();
        Debug.Log("--- Normal game started. Player 1's turn. Press C for controls. ---");
    }

    List<Card> BuildDeck()
    {
        List<Card> deck = new List<Card>();
        CommanderValue[] cvs = { CommanderValue.A, CommanderValue.J, CommanderValue.K, CommanderValue.Q };
        foreach (CommanderValue cv in cvs)
        {
            deck.Add(new Card(CardType.Commander, CardColor.Red, cv));
            deck.Add(new Card(CardType.Commander, CardColor.Red, cv));
            deck.Add(new Card(CardType.Commander, CardColor.Black, cv));
            deck.Add(new Card(CardType.Commander, CardColor.Black, cv));
        }
        for (int v = 2; v <= 10; v++)
        {
            deck.Add(new Card(CardType.Army, CardColor.Red, CommanderValue.None, v));
            deck.Add(new Card(CardType.Army, CardColor.Red, CommanderValue.None, v));
            deck.Add(new Card(CardType.Army, CardColor.Black, CommanderValue.None, v));
            deck.Add(new Card(CardType.Army, CardColor.Black, CommanderValue.None, v));
        }
        deck.Add(new Card(CardType.Joker, CardColor.Red));
        deck.Add(new Card(CardType.Joker, CardColor.Black));
        return deck;
    }

    void ShuffleDeck(List<Card> deck)
    {
        for (int i = deck.Count - 1; i > 0; i--)
        { int r = Random.Range(0, i + 1); Card t = deck[i]; deck[i] = deck[r]; deck[r] = t; }
    }

    // -------------------------------------------------------
    // SCENARIO HELPERS
    // -------------------------------------------------------

    // Builds the complete pool of all 54 non-Trumpet cards.
    List<Card> BuildFullDebugCardPool()
    {
        return BuildDeck(); // Reuses existing method — returns 54 cards
    }

    // Finds and removes the first matching Commander card from the pool.
    Card TakeCommander(List<Card> pool, CommanderValue val, CardColor color)
    {
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i].Type == CardType.Commander &&
                pool[i].CommanderVal == val &&
                pool[i].Color == color)
            {
                Card c = pool[i]; pool.RemoveAt(i); return c;
            }
        }
        Debug.LogError($"TakeCommander: Could not find {val} {color} in pool!");
        return null;
    }

    // Finds and removes the first matching Army card from the pool.
    Card TakeArmy(List<Card> pool, int value, CardColor color)
    {
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i].Type == CardType.Army &&
                pool[i].ArmyValue == value &&
                pool[i].Color == color)
            {
                Card c = pool[i]; pool.RemoveAt(i); return c;
            }
        }
        Debug.LogError($"TakeArmy: Could not find Army {value} {color} in pool!");
        return null;
    }

    // Finds and removes the Joker of the given color from the pool.
    Card TakeJoker(List<Card> pool, CardColor color)
    {
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i].Type == CardType.Joker && pool[i].Color == color)
            { Card c = pool[i]; pool.RemoveAt(i); return c; }
        }
        Debug.LogError($"TakeJoker: Could not find {color} Joker in pool!");
        return null;
    }

    // Resets everything and prepares for a fresh scenario.
    void ClearAllGameState()
    {
        Players.Clear();
        for (int i = 0; i < 4; i++) Players.Add(new PlayerState(i + 1));
        LeftDrawPile.Clear();
        RightDrawPile.Clear();
        ExilePile.Clear();
        DestroyedCards.Clear();
        TrumpetCard = new Card(CardType.Trumpet, CardColor.None);
        TrumpetInCenter = true;
        gameOver = false;
        currentPlayerIndex = 0;
        ResetAllSelections();
        ResetAllSpecialStates();
    }

    // Dumps all leftover pool cards into DestroyedCards so safety check passes.
    // For scenarios that need drawable piles, call the appropriate variant below.
    void DumpRemainingToDestroyed(List<Card> pool)
    {
        foreach (Card c in pool) DestroyedCards.Add(c);
        pool.Clear();
    }

    // Splits remaining pool into left (half) and right (other half), and dumps the rest.
    void DumpRemainingToDrawPiles(List<Card> pool)
    {
        int half = pool.Count / 2;
        for (int i = 0; i < half; i++) { LeftDrawPile.Add(pool[0]); pool.RemoveAt(0); }
        while (pool.Count > 0) { RightDrawPile.Add(pool[0]); pool.RemoveAt(0); }
    }

    void ResetAllSelections()
    {
        selectedCardIndex = -1;
        selectedOwnFrontlineIndex = -1;
        targetPlayerIndex = -1;
        targetFrontlineIndex = -1;
    }

    void ResetAllSpecialStates()
    {
        mustResolveSuperAttack = false;
        superAttackOwnerPlayerIndex = -1;
        superAttackOwnerFrontlineIndex = -1;
        isForcedDrawActive = false;
        forcedDrawPlayerIndex = -1;
        forcedDrawRemaining = 0;
        afterForcedDrawReturnToSamePlayer = false;
    }

    void PrintScenarioHeader(string name, string purpose)
    {
        Debug.Log($"\n========== SCENARIO: {name} ==========");
        Debug.Log($"PURPOSE: {purpose}");
    }

    void PrintNextSteps(string steps)
    {
        Debug.Log($"NEXT STEPS:\n{steps}");
    }

    // -------------------------------------------------------
    // F1 — NORMAL RANDOM GAME
    // -------------------------------------------------------

    void ScenarioNormalGame()
    {
        PrintScenarioHeader("F1 — Normal Random Game", "Reset to a fresh shuffled game.");
        SetupNormalGame();
        PrintNextSteps("Press H to see your hand.\nUse normal controls. Press C for help.");
        RunCardCountSafetyCheck();
    }

    // -------------------------------------------------------
    // F2 — BASIC FRONTLINE TEST
    // -------------------------------------------------------

    void ScenarioBasicFrontlineTest()
    {
        PrintScenarioHeader("F2 — Basic Frontline Test", "Test opening a frontline and adding army cards.");
        ClearAllGameState();
        List<Card> pool = BuildFullDebugCardPool();

        // Give Player 1 specific cards
        Players[0].Hand.Add(TakeCommander(pool, CommanderValue.A, CardColor.Red));
        Players[0].Hand.Add(TakeArmy(pool, 3, CardColor.Red));
        Players[0].Hand.Add(TakeArmy(pool, 5, CardColor.Red));
        Players[0].Hand.Add(TakeArmy(pool, 4, CardColor.Black));

        // Remaining cards go to other players and piles
        DumpRemainingToDrawPiles(pool);

        currentPlayerIndex = 0;
        RunCardCountSafetyCheck();
        PrintScenarioHeader("F2 board ready", "");
        Players[0].PrintHandWithIndexes();
        PrintNextSteps(
            "Press 0 → select Red A Commander\n" +
            "Press O → open frontline (Frontline 0 created)\n" +
            "Press H → see updated hand (Red 3, Red 5, Black 4 remain)\n" +
            "Press 0 → select Red Army 3\n" +
            "Press Q → select own frontline 0\n" +
            "Press A → add Red 3 to frontline (should succeed)\n" +
            "Press 0 → select Black Army 4\n" +
            "Press A → try adding Black 4 to Red frontline (should FAIL: color mismatch)"
        );
    }

    // -------------------------------------------------------
    // F3 — VALID WEAK ATTACK TEST
    // -------------------------------------------------------

    void ScenarioValidWeakAttack()
    {
        PrintScenarioHeader("F3 — Valid Weak Attack Test", "Player 1 A-frontline attacks Player 2 J-frontline. A→J is valid. Attacker power (15) > defender power (11).");
        ClearAllGameState();
        List<Card> pool = BuildFullDebugCardPool();

        // Player 1 frontline: A Red, 7 Red, 8 Red → total 15
        Card p1cmd = TakeCommander(pool, CommanderValue.A, CardColor.Red);
        Card p1a1 = TakeArmy(pool, 7, CardColor.Red);
        Card p1a2 = TakeArmy(pool, 8, CardColor.Red);
        Players[0].Frontlines.Add(new FrontlineState(p1cmd));
        Players[0].Frontlines[0].AddArmy(p1a1);
        Players[0].Frontlines[0].AddArmy(p1a2);

        // Player 2 frontline: J Red, 5 Red, 6 Red → total 11
        Card p2cmd = TakeCommander(pool, CommanderValue.J, CardColor.Red);
        Card p2a1 = TakeArmy(pool, 5, CardColor.Red);
        Card p2a2 = TakeArmy(pool, 6, CardColor.Red);
        Players[1].Frontlines.Add(new FrontlineState(p2cmd));
        Players[1].Frontlines[0].AddArmy(p2a1);
        Players[1].Frontlines[0].AddArmy(p2a2);

        DumpRemainingToDrawPiles(pool);

        currentPlayerIndex = 0;
        selectedOwnFrontlineIndex = 0;
        targetPlayerIndex = 1;
        targetFrontlineIndex = 0;

        RunCardCountSafetyCheck();
        Players[0].PrintFrontlines();
        Players[1].PrintFrontlines();
        PrintNextSteps(
            "Own frontline 0 and target already pre-selected.\n" +
            "Press B → confirm target is Player 2 Frontline 0\n" +
            "Press V → perform Weak Attack\n" +
            "EXPECTED: Player 1 loses last army (8 Red). Player 2 loses both army cards (5+6=2 cards).\n" +
            "Player 2 must forced draw 2 cards (press L or R twice).\n" +
            "Attacker's lost card does NOT count toward forced draw amount.\n" +
            "After forced draw, turn advances to Player 2."
        );
    }

    // -------------------------------------------------------
    // F4 — INVALID WEAK ATTACK TEST
    // -------------------------------------------------------

    void ScenarioInvalidWeakAttack()
    {
        PrintScenarioHeader("F4 — Invalid Weak Attack Test", "A→J is the right relationship, but attacker power (3) is NOT greater than defender power (8). Should be BLOCKED.");
        ClearAllGameState();
        List<Card> pool = BuildFullDebugCardPool();

        // Player 1: A Red, Army 3 Red → total 3
        Card p1cmd = TakeCommander(pool, CommanderValue.A, CardColor.Red);
        Card p1a1 = TakeArmy(pool, 3, CardColor.Red);
        Players[0].Frontlines.Add(new FrontlineState(p1cmd));
        Players[0].Frontlines[0].AddArmy(p1a1);

        // Player 2: J Red, Army 8 Red → total 8
        Card p2cmd = TakeCommander(pool, CommanderValue.J, CardColor.Red);
        Card p2a1 = TakeArmy(pool, 8, CardColor.Red);
        Players[1].Frontlines.Add(new FrontlineState(p2cmd));
        Players[1].Frontlines[0].AddArmy(p2a1);

        DumpRemainingToDrawPiles(pool);

        currentPlayerIndex = 0;
        selectedOwnFrontlineIndex = 0;
        targetPlayerIndex = 1;
        targetFrontlineIndex = 0;

        RunCardCountSafetyCheck();
        Players[0].PrintFrontlines();
        Players[1].PrintFrontlines();
        PrintNextSteps(
            "Press B → confirm target\n" +
            "Press V → attempt Weak Attack\n" +
            "EXPECTED: BLOCKED. Attacker power 3 is not greater than defender power 8.\n" +
            "Turn should NOT advance."
        );
    }

    // -------------------------------------------------------
    // F5 — JOKER COMMANDER WEAK ATTACK BLOCK TEST
    // -------------------------------------------------------

    void ScenarioJokerCommanderBlock()
    {
        PrintScenarioHeader("F5 — Joker Commander Weak Attack Block", "Joker commander frontlines cannot perform OR be targeted by Weak Attack.");
        ClearAllGameState();
        List<Card> pool = BuildFullDebugCardPool();

        // Player 1: Red Joker commander, 8 Red, 9 Red
        Card p1joker = TakeJoker(pool, CardColor.Red);
        Card p1a1 = TakeArmy(pool, 8, CardColor.Red);
        Card p1a2 = TakeArmy(pool, 9, CardColor.Red);
        Players[0].Frontlines.Add(new FrontlineState(p1joker));
        Players[0].Frontlines[0].AddArmy(p1a1);
        Players[0].Frontlines[0].AddArmy(p1a2);

        // Player 2: A Black, 4 Black
        Card p2cmd = TakeCommander(pool, CommanderValue.A, CardColor.Black);
        Card p2a1 = TakeArmy(pool, 4, CardColor.Black);
        Players[1].Frontlines.Add(new FrontlineState(p2cmd));
        Players[1].Frontlines[0].AddArmy(p2a1);

        DumpRemainingToDrawPiles(pool);

        currentPlayerIndex = 0;
        selectedOwnFrontlineIndex = 0;
        targetPlayerIndex = 1;
        targetFrontlineIndex = 0;

        RunCardCountSafetyCheck();
        Players[0].PrintFrontlines();
        Players[1].PrintFrontlines();
        PrintNextSteps(
            "TEST 1 — Player 1 tries to Weak Attack (Joker as attacker):\n" +
            "Press V → EXPECTED: BLOCKED (Joker commander cannot Weak Attack)\n\n" +
            "TEST 2 — Press N to skip to Player 2's turn.\n" +
            "Player 2 tries to Weak Attack Player 1's Joker frontline:\n" +
            "Press Q → select own frontline 0\n" +
            "Press Y → cycle target to Player 1\n" +
            "Press U → select target frontline 0 (Joker frontline)\n" +
            "Press V → EXPECTED: BLOCKED (Joker commander cannot be targeted by Weak Attack)"
        );
    }

    // -------------------------------------------------------
    // F6 — MANDATORY SUPER ATTACK TEST
    // -------------------------------------------------------

    void ScenarioMandatorySuperAttack()
    {
        PrintScenarioHeader("F6 — Mandatory Super Attack Test", "Adding Army 2 Red to a frontline with K Red + 9 Red + 10 Red reaches exactly 21. Mandatory Super Attack triggers.");
        ClearAllGameState();
        List<Card> pool = BuildFullDebugCardPool();

        // Player 1 frontline: K Red, 9 Red, 10 Red → total 19
        Card p1cmd = TakeCommander(pool, CommanderValue.K, CardColor.Red);
        Card p1a1 = TakeArmy(pool, 9, CardColor.Red);
        Card p1a2 = TakeArmy(pool, 10, CardColor.Red);
        Players[0].Frontlines.Add(new FrontlineState(p1cmd));
        Players[0].Frontlines[0].AddArmy(p1a1);
        Players[0].Frontlines[0].AddArmy(p1a2);

        // Player 1 hand: Army 2 Red (adding this will bring total from 19 to 21)
        Card triggerCard = TakeArmy(pool, 2, CardColor.Red);
        Players[0].Hand.Add(triggerCard);

        // Player 2 target frontline: Q Black, 5 Black, 6 Black
        Card p2cmd = TakeCommander(pool, CommanderValue.Q, CardColor.Black);
        Card p2a1 = TakeArmy(pool, 5, CardColor.Black);
        Card p2a2 = TakeArmy(pool, 6, CardColor.Black);
        Players[1].Frontlines.Add(new FrontlineState(p2cmd));
        Players[1].Frontlines[0].AddArmy(p2a1);
        Players[1].Frontlines[0].AddArmy(p2a2);

        DumpRemainingToDrawPiles(pool);

        currentPlayerIndex = 0;
        selectedCardIndex = 0;           // Red Army 2
        selectedOwnFrontlineIndex = 0;
        targetPlayerIndex = 1;
        targetFrontlineIndex = 0;

        RunCardCountSafetyCheck();
        Players[0].PrintHandWithIndexes();
        Players[0].PrintFrontlines();
        Players[1].PrintFrontlines();
        Debug.Log("NOTE: Army 2 value (2) is less than last army (10). This scenario uses the rule that adding it would reach exactly 21 via effective total check — BUT the sequence rule (value >= last) would block it normally.");
        Debug.Log("Adjusting scenario: using Army 2 on a frontline where 2 >= last army value is NOT satisfied. Let me rebuild with valid sequence...");

        // Fix: rebuild Player 1 frontline so the trigger card obeys sequence rules
        // Use: K Red, 3 Red, 9 Red → total 12. Hand: 9 Red. 12+9=21. ✓
        Players[0].Frontlines.Clear();
        DestroyedCards.Add(p1a1); // return the 9 we already took
        DestroyedCards.Add(p1a2); // return the 10
        DestroyedCards.Add(triggerCard); // return the 2
        Players[0].Hand.Clear();

        // Rebuild pool subset for new scenario
        List<Card> pool2 = BuildFullDebugCardPool();
        // Take what we need from fresh pool
        Card newCmd = TakeCommander(pool2, CommanderValue.K, CardColor.Red);
        Card newA1  = TakeArmy(pool2, 3, CardColor.Red);
        Card newA2  = TakeArmy(pool2, 9, CardColor.Red);
        Card newTrigger = TakeArmy(pool2, 9, CardColor.Red); // second Red 9

        Players[0].Frontlines.Add(new FrontlineState(newCmd));
        Players[0].Frontlines[0].AddArmy(newA1);  // 3
        Players[0].Frontlines[0].AddArmy(newA2);  // 9 → total = 12

        Players[0].Hand.Add(newTrigger); // Red 9 → adding gives 12+9=21

        // Player 2 stays as before (already has frontline from above)
        // But we must account for pool2 leftover and the earlier pool cards
        // All cards from pool2 not used go to destroyed for accounting
        // Take the same p2cmd, p2a1, p2a2 from pool2
        Card p2cmd2 = TakeCommander(pool2, CommanderValue.Q, CardColor.Black);
        Card p2a12  = TakeArmy(pool2, 5, CardColor.Black);
        Card p2a22  = TakeArmy(pool2, 6, CardColor.Black);
        // These would duplicate what Player 2 already has — but ClearAllGameState already cleared.
        // Player 2's frontline was set above and Player 2's frontline cards came from pool (original).
        // pool2 leftovers go to destroyed.
        DumpRemainingToDestroyed(pool2);

        // Recalculate: We have two pools contributing cards.
        // Total = (cards assigned from pool) + (cards assigned from pool2) + destroyed + trumpet = 55?
        // This gets complicated. Let's do a cleaner single-pool rebuild.
        // ----- CLEAN SINGLE-POOL REBUILD -----
        ClearAllGameState();
        List<Card> p = BuildFullDebugCardPool();

        Card f6_p1cmd = TakeCommander(p, CommanderValue.K, CardColor.Red);
        Card f6_p1a1  = TakeArmy(p, 3, CardColor.Red);
        Card f6_p1a2  = TakeArmy(p, 9, CardColor.Red);
        Card f6_hand  = TakeArmy(p, 9, CardColor.Red); // second Red 9

        Players[0].Frontlines.Add(new FrontlineState(f6_p1cmd));
        Players[0].Frontlines[0].AddArmy(f6_p1a1);
        Players[0].Frontlines[0].AddArmy(f6_p1a2);
        Players[0].Hand.Add(f6_hand);

        Card f6_p2cmd = TakeCommander(p, CommanderValue.Q, CardColor.Black);
        Card f6_p2a1  = TakeArmy(p, 5, CardColor.Black);
        Card f6_p2a2  = TakeArmy(p, 6, CardColor.Black);
        Players[1].Frontlines.Add(new FrontlineState(f6_p2cmd));
        Players[1].Frontlines[0].AddArmy(f6_p2a1);
        Players[1].Frontlines[0].AddArmy(f6_p2a2);

        DumpRemainingToDrawPiles(p);

        currentPlayerIndex = 0;
        selectedCardIndex = 0;           // Red Army 9 in hand
        selectedOwnFrontlineIndex = 0;
        targetPlayerIndex = 1;
        targetFrontlineIndex = 0;

        RunCardCountSafetyCheck();
        Players[0].PrintHandWithIndexes();
        Players[0].PrintFrontlines();
        Debug.Log($"Player 1 frontline effective total: {Players[0].Frontlines[0].GetEffectiveArmyTotal()} (adding hand card will make 21)");
        Players[1].PrintFrontlines();
        PrintNextSteps(
            "Card [0] (Red Army 9) and Frontline 0 already selected. Target is Player 2 Frontline 0.\n" +
            "Press A → add Red Army 9 to Frontline 0\n" +
            "EXPECTED: Total becomes 3+9+9=21. MANDATORY SUPER ATTACK activates. Normal moves BLOCKED.\n" +
            "Press B → confirm target (Player 2, Frontline 0)\n" +
            "Press S → execute Super Attack\n" +
            "EXPECTED: Player 2 frontline (Q+5+6 = 3 cards) destroyed. Player 2 forced draws 3.\n" +
            "Player 1 army cards destroyed. Commander K Red stays.\n" +
            "After forced draw: Player 1 STILL has normal move (turn does NOT advance)."
        );
    }

    // -------------------------------------------------------
    // F7 — JOKER AS ARMY DYNAMIC VALUE TEST
    // -------------------------------------------------------

    void ScenarioJokerArmyDynamicValue()
    {
        PrintScenarioHeader("F7 — Joker as Army Dynamic Value Test",
            "Frontline: A Red | Army 3 Red | Red Joker | Army 7 Red\n" +
            "Joker sits between 3 and 7, so its range is [3,7]. Max = 7.\n" +
            "Effective total = 3+7+7 = 17.\n" +
            "Adding Red 8: Joker min=3, check 3+3+7+8=21 ≤ 21 → ALLOWED (reaches 21 → Super Attack!).\n" +
            "Adding Red 9: Joker min=3, check 3+3+7+9=22 > 21 → REJECTED.");
        ClearAllGameState();
        List<Card> p = BuildFullDebugCardPool();

        Card cmd   = TakeCommander(p, CommanderValue.A, CardColor.Red);
        Card a3    = TakeArmy(p, 3, CardColor.Red);
        Card joker = TakeJoker(p, CardColor.Red);
        Card a7    = TakeArmy(p, 7, CardColor.Red);
        Card h8    = TakeArmy(p, 8, CardColor.Red);
        Card h9    = TakeArmy(p, 9, CardColor.Red);

        Players[0].Frontlines.Add(new FrontlineState(cmd));
        Players[0].Frontlines[0].AddArmy(a3);
        Players[0].Frontlines[0].AddArmy(joker);
        Players[0].Frontlines[0].AddArmy(a7);

        Players[0].Hand.Add(h8); // index 0
        Players[0].Hand.Add(h9); // index 1

        // Give Player 2 a frontline so super attack has a target
        Card p2cmd = TakeCommander(p, CommanderValue.J, CardColor.Black);
        Card p2a1  = TakeArmy(p, 4, CardColor.Black);
        Players[1].Frontlines.Add(new FrontlineState(p2cmd));
        Players[1].Frontlines[0].AddArmy(p2a1);

        DumpRemainingToDrawPiles(p);

        currentPlayerIndex = 0;
        selectedCardIndex = 0; // Red Army 8
        selectedOwnFrontlineIndex = 0;
        targetPlayerIndex = 1;
        targetFrontlineIndex = 0;

        RunCardCountSafetyCheck();
        Players[0].PrintHandWithIndexes();
        Players[0].PrintFrontlines();

        int effective = Players[0].Frontlines[0].GetEffectiveArmyTotal();
        List<int> possibles = Players[0].Frontlines[0].GetPossibleArmyTotals();
        string possStr = string.Join(", ", possibles);
        Debug.Log($"Current frontline possible totals: [{possStr}] | Effective (highest ≤21): {effective}");

        PrintNextSteps(
            "Hand: [0] Red Army 8   [1] Red Army 9\n" +
            "Frontline: A Red | 3 Red | Red Joker | 7 Red\n" +
            "Joker range [3,7], max=7. Effective total = 3+7+7 = 17.\n\n" +
            "TEST 1 — Add Red Army 8:\n" +
            "Press A (card [0] and frontline 0 already selected)\n" +
            "EXPECTED: Allowed. Min joker=3 → 3+3+7+8=21 ≤ 21. Total = 21. SUPER ATTACK triggers!\n" +
            "After Super Attack: press F7 again to reset and try Test 2.\n\n" +
            "TEST 2 — After pressing F7 again:\n" +
            "Press 1 → select Red Army 9\n" +
            "Press A → try adding\n" +
            "EXPECTED: REJECTED. Min joker=3 → 3+3+7+9=22 > 21."
        );
    }

    // -------------------------------------------------------
    // F8 — EXILE TEST
    // -------------------------------------------------------

    void ScenarioExileTest()
    {
        PrintScenarioHeader("F8 — Exile Test", "Test sending cards from frontline to exile, then drawing from exile.");
        ClearAllGameState();
        List<Card> p = BuildFullDebugCardPool();

        Card cmd = TakeCommander(p, CommanderValue.Q, CardColor.Black);
        Card a4  = TakeArmy(p, 4, CardColor.Black);
        Card a6  = TakeArmy(p, 6, CardColor.Black);
        Players[0].Frontlines.Add(new FrontlineState(cmd));
        Players[0].Frontlines[0].AddArmy(a4);
        Players[0].Frontlines[0].AddArmy(a6);

        DumpRemainingToDrawPiles(p);

        currentPlayerIndex = 0;
        selectedOwnFrontlineIndex = 0;

        RunCardCountSafetyCheck();
        Players[0].PrintFrontlines();
        PrintNextSteps(
            "Own frontline 0 already selected.\n" +
            "Press Z → exile top card (Army 6 Black goes to exile pile)\n" +
            "EXPECTED: Exile pile count becomes 1. Turn advances to Player 2.\n" +
            "Press N × 3 to skip back to Player 1, or use another player to draw.\n" +
            "Press X → draw from exile\n" +
            "EXPECTED: Army 6 Black drawn into hand. Exile pile count returns to 0.\n" +
            "Safety check should always show 55."
        );
    }

    // -------------------------------------------------------
    // F9 — FORCED DRAW TEST
    // -------------------------------------------------------

    void ScenarioForcedDrawTest()
    {
        PrintScenarioHeader("F9 — Forced Draw Test", "Player 2 is in forced draw mode and must draw 2 cards. Normal moves are blocked.");
        ClearAllGameState();
        List<Card> p = BuildFullDebugCardPool();

        // Put known cards at the top (end) of each pile
        Card leftTop   = TakeArmy(p, 5, CardColor.Red);
        Card rightTop  = TakeArmy(p, 6, CardColor.Black);
        Card exileTop  = TakeArmy(p, 7, CardColor.Red);
        ExilePile.Add(exileTop);

        DumpRemainingToDrawPiles(p);
        LeftDrawPile.Add(leftTop);   // leftTop is now top of left pile
        RightDrawPile.Add(rightTop); // rightTop is now top of right pile

        currentPlayerIndex = 0; // "Player 1's turn" display, but forced draw overrides
        StartForcedDraw(1, 2, false); // Player 2 (index 1) must draw 2, then turn advances

        RunCardCountSafetyCheck();
        PrintNextSteps(
            "Forced draw is ACTIVE for Player 2. Player 2 must draw 2 cards.\n" +
            "Normal move keys (O, A, Z, V, S) are BLOCKED.\n" +
            "Press L → Player 2 draws from Left pile (Red Army 5)\n" +
            "Press R → Player 2 draws from Right pile (Black Army 6)\n" +
            "Press X → Player 2 draws from Exile pile (Red Army 7)\n" +
            "After 2 successful draws, forced draw ends and turn advances normally.\n" +
            "Safety check should stay 55 throughout."
        );
    }

    // -------------------------------------------------------
    // F10 — TRUMPET DRAW WIN TEST
    // -------------------------------------------------------

    void ScenarioTrumpetWin()
    {
        PrintScenarioHeader("F10 — Trumpet Draw Win Test",
            "All piles empty. Player 2 has the highest army strength (15). Press T to draw Trumpet and win.");
        ClearAllGameState();
        List<Card> p = BuildFullDebugCardPool();

        // Build frontlines for each player with known army totals
        // Player 1: total 10
        Card p1cmd = TakeCommander(p, CommanderValue.A, CardColor.Red);
        Card p1a1  = TakeArmy(p, 10, CardColor.Red);
        Players[0].Frontlines.Add(new FrontlineState(p1cmd));
        Players[0].Frontlines[0].AddArmy(p1a1);

        // Player 2: total 15
        Card p2cmd = TakeCommander(p, CommanderValue.J, CardColor.Red);
        Card p2a1  = TakeArmy(p, 7, CardColor.Red);
        Card p2a2  = TakeArmy(p, 8, CardColor.Red);
        Players[1].Frontlines.Add(new FrontlineState(p2cmd));
        Players[1].Frontlines[0].AddArmy(p2a1);
        Players[1].Frontlines[0].AddArmy(p2a2);

        // Player 3: total 8
        Card p3cmd = TakeCommander(p, CommanderValue.K, CardColor.Black);
        Card p3a1  = TakeArmy(p, 8, CardColor.Black);
        Players[2].Frontlines.Add(new FrontlineState(p3cmd));
        Players[2].Frontlines[0].AddArmy(p3a1);

        // Player 4: total 12
        Card p4cmd = TakeCommander(p, CommanderValue.Q, CardColor.Black);
        Card p4a1  = TakeArmy(p, 5, CardColor.Black);
        Card p4a2  = TakeArmy(p, 7, CardColor.Black);
        Players[3].Frontlines.Add(new FrontlineState(p4cmd));
        Players[3].Frontlines[0].AddArmy(p4a1);
        Players[3].Frontlines[0].AddArmy(p4a2);

        // All remaining cards go to destroyed (piles must be EMPTY for Trumpet draw)
        DumpRemainingToDestroyed(p);
        // Piles are already empty from ClearAllGameState

        currentPlayerIndex = 0;
        TrumpetInCenter = true;

        RunCardCountSafetyCheck();
        for (int i = 0; i < Players.Count; i++)
        {
            Debug.Log($"Player {Players[i].PlayerIndex} army strength: {Players[i].GetTotalArmyStrength()}");
            Players[i].PrintFrontlines();
        }
        Debug.Log($"Left pile: {LeftDrawPile.Count} | Right pile: {RightDrawPile.Count} | Exile: {ExilePile.Count} — all empty ✓");
        PrintNextSteps(
            "All draw piles are empty. Trumpet is in center.\n" +
            "Press T → draw Trumpet\n" +
            "EXPECTED: Game calculates army strength for all players.\n" +
            "Player 1=10, Player 2=15, Player 3=8, Player 4=12.\n" +
            "WINNER: Player 2 (15 is highest). gameOver = true. Further moves blocked."
        );
    }

    // -------------------------------------------------------
    // F11 — EMPTY HAND WIN TEST
    // -------------------------------------------------------

    void ScenarioEmptyHandWin()
    {
        PrintScenarioHeader("F11 — Empty Hand Win Test", "Player 1 has exactly 1 card. Opening a frontline with it empties the hand. Player 1 wins.");
        ClearAllGameState();
        List<Card> p = BuildFullDebugCardPool();

        Card winCard = TakeCommander(p, CommanderValue.A, CardColor.Red);
        Players[0].Hand.Add(winCard);

        DumpRemainingToDrawPiles(p);

        currentPlayerIndex = 0;
        selectedCardIndex = 0;

        RunCardCountSafetyCheck();
        Players[0].PrintHandWithIndexes();
        PrintNextSteps(
            "Player 1 has exactly 1 card: Red A Commander. Card [0] already selected.\n" +
            "Press O → open frontline\n" +
            "EXPECTED: Frontline opens. Hand becomes empty. PLAYER 1 WINS! gameOver = true.\n" +
            "Further moves should be BLOCKED (except F-keys and P/C)."
        );
    }

    // -------------------------------------------------------
    // F12 — PRINT SCENARIO HELP
    // -------------------------------------------------------

    void PrintScenarioHelp()
    {
        Debug.Log("========== DEBUG SCENARIO KEYS ==========\n" +
                  "F1  = Reset to normal random game\n" +
                  "F2  = Basic frontline test (open + add army)\n" +
                  "F3  = Valid weak attack test (A→J, power 15 vs 11)\n" +
                  "F4  = Invalid weak attack test (power too low)\n" +
                  "F5  = Joker commander weak attack block\n" +
                  "F6  = Mandatory Super Attack test (reaching 21)\n" +
                  "F7  = Joker as army dynamic value test\n" +
                  "F8  = Exile test (send to exile + draw from exile)\n" +
                  "F9  = Forced draw mode test\n" +
                  "F10 = Trumpet draw win test\n" +
                  "F11 = Empty hand win test\n" +
                  "F12 = Print this help\n" +
                  "==========================================");
    }

    // -------------------------------------------------------
    // PRINTING
    // -------------------------------------------------------

    void PrintSetupReport()
    {
        Debug.Log("========== GAME SETUP REPORT ==========");
        foreach (PlayerState pl in Players) pl.PrintHandWithIndexes();
        Debug.Log($"Left: {LeftDrawPile.Count} | Right: {RightDrawPile.Count} | Exile: {ExilePile.Count} | Destroyed: {DestroyedCards.Count}");
        Debug.Log($"Trumpet in center: {TrumpetInCenter}");
        RunCardCountSafetyCheck();
        Debug.Log("========================================");
    }

    void PrintFullGameState()
    {
        Debug.Log("========== FULL GAME STATE ==========");
        Debug.Log($"Game Over: {gameOver}");
        Debug.Log($"Current Player: Player {Players[currentPlayerIndex].PlayerIndex}");
        Debug.Log($"Forced Draw Active: {isForcedDrawActive}" +
                  (isForcedDrawActive ? $" | Player: {Players[forcedDrawPlayerIndex].PlayerIndex} | Remaining: {forcedDrawRemaining}" : ""));
        Debug.Log($"Mandatory Super Attack: {mustResolveSuperAttack}" +
                  (mustResolveSuperAttack ? $" | Owner: Player {Players[superAttackOwnerPlayerIndex].PlayerIndex} FL{superAttackOwnerFrontlineIndex}" : ""));
        Debug.Log($"Selected: Card={selectedCardIndex} OwnFL={selectedOwnFrontlineIndex} TargetPlayer={targetPlayerIndex} TargetFL={targetFrontlineIndex}");
        Debug.Log("---");
        foreach (PlayerState pl in Players)
        {
            Debug.Log($"Player {pl.PlayerIndex}: Hand={pl.Hand.Count} | Frontlines={pl.Frontlines.Count} | ArmyStrength={pl.GetTotalArmyStrength()}");
            pl.PrintFrontlines();
        }
        Debug.Log("---");
        Debug.Log($"Left: {LeftDrawPile.Count} | Right: {RightDrawPile.Count} | Exile: {ExilePile.Count} | Destroyed: {DestroyedCards.Count} | Trumpet in center: {TrumpetInCenter}");
        RunCardCountSafetyCheck();
        Debug.Log("=====================================");
    }

    void PrintControls()
    {
        Debug.Log("========== CONTROLS ==========\n" +
                  "GENERAL:\n" +
                  "  C = controls help   P = full state   H = hand   F = frontlines   N = skip turn (debug)\n\n" +
                  "SELECTION:\n" +
                  "  0-9 = select hand card\n" +
                  "  Q/W/E = select own frontline 0/1/2\n" +
                  "  Y = cycle target player   U = cycle target frontline   B = print target\n\n" +
                  "MOVES:\n" +
                  "  L = draw left pile   R = draw right pile   X = draw exile   T = draw Trumpet\n" +
                  "  O = open frontline   A = add army card   Z = exile top card\n" +
                  "  V = weak attack   S = super attack\n\n" +
                  "DEBUG SCENARIOS (always available):\n" +
                  "  F1=Normal  F2=Frontline  F3=ValidWeakAtk  F4=InvalidWeakAtk  F5=JokerBlock\n" +
                  "  F6=SuperAtk  F7=JokerArmy  F8=Exile  F9=ForcedDraw  F10=Trumpet  F11=EmptyHand  F12=ScenarioHelp\n" +
                  "==============================");
    }

    // -------------------------------------------------------
    // SAFETY CHECK
    // -------------------------------------------------------

    void RunCardCountSafetyCheck()
    {
        int total = 0;
        foreach (PlayerState pl in Players) total += pl.CountAllCards();
        total += LeftDrawPile.Count;
        total += RightDrawPile.Count;
        total += ExilePile.Count;
        total += DestroyedCards.Count;
        if (TrumpetInCenter) total += 1;
        // If Trumpet was drawn, it's already in a player's hand and counted above

        if (total == 55)
            Debug.Log($"[Safety Check] {total}/55 ✓");
        else
            Debug.LogError($"[Safety Check] MISMATCH! Found {total}, expected 55!");
    }
}