// Bu dosya her oyuncunun durumunu tutar.
// Elin hangi kartlar var, kaç cephen var vs.

using System.Collections.Generic;
using System.Linq;

public class PlayerState
{
    public int PlayerId { get; set; }             // Oyuncu numarası (0=insan, 1-2-3=bot)
    public string PlayerName { get; set; }        // Görünen isim
    public List<Card> Hand { get; set; }          // Eldeki kartlar
    public List<Cephe> Cepheler { get; set; }     // Açık cepheler (max 3)
    public bool IsBot { get; set; }               // Bot mu?

    public PlayerState(int id, string name, bool isBot)
    {
        PlayerId = id;
        PlayerName = name;
        IsBot = isBot;
        Hand = new List<Card>();
        Cepheler = new List<Cephe>();
    }

    // Eldeki komutanları getir
    public List<Card> GetCommandersInHand()
    {
        // Komutan kartları ve joker kartları cephe açabilir
        return Hand.Where(c => c.Type == CardType.Commander || c.IsJoker).ToList();
    }

    // Eldeki ordu kartlarını getir
    public List<Card> GetArmyCardsInHand()
    {
        return Hand.Where(c => c.Type == CardType.Army).ToList();
    }
        // Oyuncu durumunun geçerli olup olmadığını kontrol eder
    public bool IsValid()
    {
        // PlayerName null veya boş olmamalı
        if (string.IsNullOrEmpty(PlayerName))
            return false;

        // PlayerId geçerli aralıkta olmalı (0-3)
        if (PlayerId < 0 || PlayerId > 3)
            return false;

        // Hand ve Cepheler null olmamalı
        if (Hand == null || Cepheler == null)
            return false;

        // Maksimum 3 cephe olabilir
        if (Cepheler.Count > 3)
            return false;

        // Her cephe geçerli olmalı
        foreach (var cephe in Cepheler)
        {
            // Cephe ve Army null olmamalı
            if (cephe == null || cephe.Army == null)
                return false;
            
            // Commander varsa geçerli olmalı (null olabilir ama varsa doğru olmalı)
            if (cephe.Commander != null && !cephe.Commander.IsValid())
                return false;
}

        return true;
    }

}