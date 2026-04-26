// Bu dosya oyunun tamamının anlık durumunu tutar.
// Kimin sırası, ortadaki kartlar, kim ne oynuyor vs.

using System.Collections.Generic;

public class GameState
{
    public List<PlayerState> Players { get; set; }      // 4 oyuncu (1 insan + 3 bot)
    public List<Card> LeftDeck { get; set; }            // Sol kart destesi
    public List<Card> RightDeck { get; set; }           // Sağ kart destesi
    public List<Card> TrompetPile { get; set; }         // Trompet üstündeki kartlar
    public int CurrentPlayerIndex { get; set; }         // Şu an kimin sırası (0-3)

    public GameState()
    {
        Players = new List<PlayerState>();
        LeftDeck = new List<Card>();
        RightDeck = new List<Card>();
        TrompetPile = new List<Card>();
        CurrentPlayerIndex = 0;
    }

    // Sol destenin en üst kartı
    public Card LeftTopCard()
    {
        if (LeftDeck.Count > 0)
            return LeftDeck[LeftDeck.Count - 1];
        return null;
    }

    // Sağ destenin en üst kartı
    public Card RightTopCard()
    {
        if (RightDeck.Count > 0)
            return RightDeck[RightDeck.Count - 1];
        return null;
    }

    // Sırayı bir sonraki oyuncuya geçir
    public void NextTurn()
    {
        CurrentPlayerIndex = (CurrentPlayerIndex + 1) % Players.Count;
    }

    // Şu an oynayan oyuncuyu getir
    public PlayerState CurrentPlayer()
    {
        return Players[CurrentPlayerIndex];
    }


    // Oyun durumunun geçerli olup olmadığını kontrol eder
    public bool IsValid()
    {
        // Oyuncu listesi null veya boş olmamalı
        if (Players == null || Players.Count == 0)
            return false;

        // Desteler null olmamalı
        if (LeftDeck == null || RightDeck == null || TrompetPile == null)
            return false;

        // CurrentPlayerIndex geçerli aralıkta olmalı
        if (CurrentPlayerIndex < 0 || CurrentPlayerIndex >= Players.Count)
            return false;

        // Her oyuncu geçerli olmalı
        foreach (var player in Players)
        {
            if (player == null || player.Hand == null || player.Cepheler == null)
                return false;
        }

        return true;
    }

}