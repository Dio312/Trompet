
// Bu dosya bir kartın ne olduğunu tanımlar.
// Örnek: Kırmızı renkte, değeri 7 olan bir ordu kartı.

public enum CardType
{
    Commander,  // Komutan (A, J, K, Q)
    Army,       // Ordu (2'den 10'a)
    Joker,      // Joker
    Trumpet     // Trompet
}
public enum CardColor
{
    Red,    // Kırmızı
    Black,  // Siyah
    None    // Renksiz (Trompet kartı için)
}


public class Card
{
    public CardType Type { get; set; }         // Kartın tipi
    public CardColor Color { get; set; }        // Kartın rengi (Trompet için None)
    public int ArmyValue { get; set; }    // Ordu kartlarında değer (2-10), diğerlerinde 0
    public string CommanderName { get; set; } // Komutan: "A","J","K","Q" | Joker: "Joker" | Trompet: "Trompet"
    public bool IsJoker => Type == CardType.Joker;


    // Kartı ekranda göstermek için
    public override string ToString()
    {
        if (IsJoker) return $"{Color} Joker";
        if (Type == CardType.Commander) return $"Komutan {CommanderName} ({Color})";
        if (Type == CardType.Army) return $"Ordu {ArmyValue} ({Color})";
        if (Type == CardType.Trumpet) return "Trompet";
        return "Bilinmeyen Kart";
    }
        // Kartın geçerli olup olmadığını kontrol eder
    public bool IsValid()
    {
        // CommanderName null olmamalı
        // CommanderName kontrolü - Ordu kartları için boş string kabul edilir
        if (Type != CardType.Army && string.IsNullOrEmpty(CommanderName))
            return false;

        // Ordu kartları için değer 2-10 arasında olmalı
        if (Type == CardType.Army && (ArmyValue < 2 || ArmyValue > 10))
            return false;

        // Komutan ve Joker kartları için ArmyValue 0 olmalı
        if ((Type == CardType.Commander || Type == CardType.Joker) && ArmyValue != 0)
            return false;

        // Trompet kartı renksiz olmalı
        if (Type == CardType.Trumpet && Color != CardColor.None)
            return false;

        // Diğer kartlar renksiz olmamalı
        if (Type != CardType.Trumpet && Color == CardColor.None)
            return false;

        return true;
    }
    

}