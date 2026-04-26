// Card.cs
// Represents a single card in the game.

public enum CardType
{
    Army,
    Commander,
    Joker,
    Trumpet
}

public enum CardColor
{
    Red,
    Black,
    None  // Used for the Trumpet card
}

public enum CommanderValue
{
    None,  // Used for non-commander cards
    A,
    J,
    K,
    Q
}

public class Card
{
    public CardType Type;
    public CardColor Color;
    public CommanderValue CommanderVal;
    public int ArmyValue;  // 2–10 for army cards, 0 for all others

    // Constructor
    public Card(CardType type, CardColor color, CommanderValue commanderVal = CommanderValue.None, int armyValue = 0)
    {
        Type = type;
        Color = color;
        CommanderVal = commanderVal;
        ArmyValue = armyValue;
    }

    // Readable name for Debug.Log
    public override string ToString()
    {
        switch (Type)
        {
            case CardType.Trumpet:
                return "[Trumpet]";
            case CardType.Joker:
                return $"[Joker | {Color}]";
            case CardType.Commander:
                return $"[Commander | {CommanderVal} | {Color}]";
            case CardType.Army:
                return $"[Army | {ArmyValue} | {Color}]";
            default:
                return "[Unknown Card]";
        }
    }
}