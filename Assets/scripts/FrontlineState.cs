// FrontlineState.cs
// Represents one frontline (cephe) belonging to a player.
// Phase 4: Added Joker-as-army dynamic value calculation.

using System.Collections.Generic;
using UnityEngine;

public class FrontlineState
{
    public Card Commander;
    public List<Card> ArmyCards;
    public bool IsJokerCommander;

    public FrontlineState(Card commanderCard)
    {
        Commander = commanderCard;
        ArmyCards = new List<Card>();
        IsJokerCommander = (commanderCard.Type == CardType.Joker);
    }

    // -------------------------------------------------------
    // ARMY TOTAL CALCULATIONS
    // -------------------------------------------------------

    // Returns every possible army total this frontline could have,
    // given that Joker cards can take values 2-10 within sequence rules.
    // We use recursive enumeration over the army card list.
    public List<int> GetPossibleArmyTotals()
    {
        // Start recursion: possible totals so far = {0}, previous value = 0
        List<int> results = new List<int>();
        EnumerateTotals(0, 0, 10, results);
        return results;
    }

    // Recursive helper. index = which army card we're evaluating.
    // runningTotal = sum so far. prevMin = minimum value current card must be >= .
    // prevMax = maximum value current card can be (set by the next non-Joker card, or 10).
    private void EnumerateTotals(int index, int runningTotal, int prevMax, List<int> results)
    {
        if (index == ArmyCards.Count)
        {
            results.Add(runningTotal);
            return;
        }

        Card card = ArmyCards[index];

        if (card.Type == CardType.Army)
        {
            // Fixed value — must be >= previous card's value
            // (sequence validity is enforced at add time, so just use it)
            EnumerateTotals(index + 1, runningTotal + card.ArmyValue, 10, results);
        }
        else if (card.Type == CardType.Joker)
        {
            // Find the minimum the joker must be: value of the previous non-joker card,
            // or 2 if this is the first card.
            int jokerMin = GetMinJokerValue(index);
            int jokerMax = GetMaxJokerValue(index);

            for (int v = jokerMin; v <= jokerMax; v++)
            {
                EnumerateTotals(index + 1, runningTotal + v, 10, results);
            }
        }
    }

    // Minimum value the joker at 'index' can take:
    // must be >= the value of the previous army card (or 2 if none).
    private int GetMinJokerValue(int jokerIndex)
    {
        for (int i = jokerIndex - 1; i >= 0; i--)
        {
            if (ArmyCards[i].Type == CardType.Army)
                return ArmyCards[i].ArmyValue;
            // If previous was also a joker, keep searching
        }
        return 2; // no previous card
    }

    // Maximum value the joker at 'index' can take:
    // must be <= the value of the next army card (or 10 if none after).
    private int GetMaxJokerValue(int jokerIndex)
    {
        for (int i = jokerIndex + 1; i < ArmyCards.Count; i++)
        {
            if (ArmyCards[i].Type == CardType.Army)
                return ArmyCards[i].ArmyValue;
        }
        return 10; // no next fixed card
    }

    // Returns the highest legal army total that does not exceed 21.
    // Returns 0 if the frontline has no army cards.
    public int GetEffectiveArmyTotal()
    {
        if (ArmyCards.Count == 0) return 0;

        List<int> possibles = GetPossibleArmyTotals();
        int best = -1;
        foreach (int t in possibles)
        {
            if (t <= 21 && t > best)
                best = t;
        }
        return best < 0 ? 0 : best;
    }

    // Returns true if adding this card would make the effective total exactly 21.
    public bool WouldReachExactly21AfterAdding(Card card)
    {
        // Temporarily add, check, then remove
        ArmyCards.Add(card);
        int effective = GetEffectiveArmyTotal();
        ArmyCards.RemoveAt(ArmyCards.Count - 1);
        return effective == 21;
    }

    // -------------------------------------------------------
    // VALIDATION
    // -------------------------------------------------------

    public bool CanAddArmy(Card card, out string reason)
    {
        // Must be Army or Joker
        if (card.Type != CardType.Army && card.Type != CardType.Joker)
        {
            reason = $"Only Army or Joker cards can be added as army. You selected: {card}";
            return false;
        }

        // Color must match commander
        if (card.Color != Commander.Color)
        {
            reason = $"Color mismatch. Commander is {Commander.Color}, card is {card.Color}.";
            return false;
        }

        // Value sequence check for normal army cards
        if (card.Type == CardType.Army && ArmyCards.Count > 0)
        {
            // Find the effective minimum: last non-joker army value,
            // or the min the last joker could be.
            int lastEffectiveMin = GetLastEffectiveMinValue();
            if (card.ArmyValue < lastEffectiveMin)
            {
                reason = $"Army value {card.ArmyValue} is less than last effective minimum {lastEffectiveMin}.";
                return false;
            }
        }

        // For Joker as army: check sequence feasibility
        if (card.Type == CardType.Joker && ArmyCards.Count > 0)
        {
            // Joker min value it could take = last effective min
            int lastEffectiveMin = GetLastEffectiveMinValue();
            if (10 < lastEffectiveMin) // joker max is always 10
            {
                reason = $"Joker cannot satisfy sequence rule. Last card min is {lastEffectiveMin}, Joker max is 10.";
                return false;
            }
        }

        // Check that adding this card leaves at least one valid total <= 21
        ArmyCards.Add(card);
        List<int> possibles = GetPossibleArmyTotals();
        ArmyCards.RemoveAt(ArmyCards.Count - 1);

        bool anyLegal = false;
        foreach (int t in possibles)
        {
            if (t <= 21) { anyLegal = true; break; }
        }

        if (!anyLegal)
        {
            reason = $"Adding {card} would push all possible totals above 21.";
            return false;
        }

        reason = "";
        return true;
    }

    // Returns the effective minimum value the last card in the army list represents.
    private int GetLastEffectiveMinValue()
    {
        if (ArmyCards.Count == 0) return 2;
        Card last = ArmyCards[ArmyCards.Count - 1];
        if (last.Type == CardType.Army) return last.ArmyValue;
        // Last is a Joker — its min is the card before it
        return GetMinJokerValue(ArmyCards.Count - 1);
    }

    // -------------------------------------------------------
    // MUTATION
    // -------------------------------------------------------

    public void AddArmy(Card card)
    {
        ArmyCards.Add(card);
    }

    // Removes and returns the last card in this frontline (army or commander).
    // Returns null if frontline is empty (should not happen normally).
    public Card RemoveTopCard()
    {
        if (ArmyCards.Count > 0)
        {
            Card top = ArmyCards[ArmyCards.Count - 1];
            ArmyCards.RemoveAt(ArmyCards.Count - 1);
            return top;
        }
        // No army cards — remove commander (frontline destroyed)
        return null; // Caller handles frontline destruction
    }

    // -------------------------------------------------------
    // PRINTING
    // -------------------------------------------------------

    public void PrintFrontline(int frontlineIndex)
    {
        int effective = GetEffectiveArmyTotal();
        string commanderLabel = IsJokerCommander ? $"{Commander} (Joker Commander)" : Commander.ToString();
        string output = $"  Frontline {frontlineIndex}: Commander = {commanderLabel}\n";
        output += $"    Army Cards ({ArmyCards.Count}):\n";

        if (ArmyCards.Count == 0)
        {
            output += "      (none)\n";
        }
        else
        {
            for (int i = 0; i < ArmyCards.Count; i++)
            {
                output += $"      {i}: {ArmyCards[i]}\n";
            }
        }

        output += $"    Effective Army Total: {effective}";
        if (effective == 21) output += " *** SUPER ATTACK READY ***";
        Debug.Log(output);
    }
}