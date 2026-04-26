// PlayerState.cs
// Stores a single player's hand, frontlines, and helper methods.

using System.Collections.Generic;
using UnityEngine;

public class PlayerState
{
    public int PlayerIndex;
    public List<Card> Hand;
    public List<FrontlineState> Frontlines;

    private const int MaxFrontlines = 3;

    public PlayerState(int index)
    {
        PlayerIndex = index;
        Hand = new List<Card>();
        Frontlines = new List<FrontlineState>();
    }

    // -------------------------------------------------------
    // HAND PRINTING
    // -------------------------------------------------------

    public void PrintHandWithIndexes()
    {
        string output = $"--- Player {PlayerIndex} Hand ({Hand.Count} cards) ---\n";
        if (Hand.Count == 0)
        {
            output += "  (empty hand)\n";
        }
        else
        {
            for (int i = 0; i < Hand.Count; i++)
                output += $"  [{i}] {Hand[i]}\n";
        }
        Debug.Log(output);
    }

    public void PrintHand() { PrintHandWithIndexes(); }

    // -------------------------------------------------------
    // FRONTLINE PRINTING
    // -------------------------------------------------------

    public void PrintFrontlines()
    {
        Debug.Log($"--- Player {PlayerIndex} Frontlines ({Frontlines.Count}/{MaxFrontlines}) ---");
        if (Frontlines.Count == 0)
        {
            Debug.Log("  (no frontlines open)");
        }
        else
        {
            for (int i = 0; i < Frontlines.Count; i++)
                Frontlines[i].PrintFrontline(i);
        }
    }

    // -------------------------------------------------------
    // FRONTLINE MANAGEMENT
    // -------------------------------------------------------

    public bool CanOpenFrontline(Card card, out string reason)
    {
        if (Frontlines.Count >= MaxFrontlines)
        {
            reason = $"Player {PlayerIndex} already has {MaxFrontlines} frontlines.";
            return false;
        }
        if (card.Type != CardType.Commander && card.Type != CardType.Joker)
        {
            reason = $"Only Commander or Joker cards can open a frontline. Selected: {card}";
            return false;
        }
        reason = "";
        return true;
    }

    public void OpenFrontline(Card card)
    {
        Hand.Remove(card);
        Frontlines.Add(new FrontlineState(card));
    }

    // -------------------------------------------------------
    // ARMY TOTAL (for Trumpet win condition)
    // -------------------------------------------------------

    public int GetTotalArmyStrength()
    {
        int total = 0;
        foreach (FrontlineState f in Frontlines)
            total += f.GetEffectiveArmyTotal();
        return total;
    }

    // -------------------------------------------------------
    // CARD COUNTING (for safety check)
    // -------------------------------------------------------

    public int CountAllCards()
    {
        int total = Hand.Count;
        foreach (FrontlineState f in Frontlines)
        {
            total += 1; // commander
            total += f.ArmyCards.Count;
        }
        return total;
    }
}