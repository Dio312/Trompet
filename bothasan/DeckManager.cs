using System.Collections.Generic;
using UnityEngine;

public class DeckManager
{
    // Tüm 55 kartlık desteyi oluşturur
    public static List<Card> CreateDeck()
    {
        List<Card> deck = new List<Card>();

        // Her renk için (Kırmızı ve Siyah)
        foreach (CardColor color in new[] { CardColor.Red, CardColor.Black })
        {
            // Komutanlar: A, J, K, Q (4 komutan x 2 renk = 8 komutan)
            // Ama oyunda toplam 16 komutan var, yani her renkte 8 tane
            foreach (string name in new[] { "A", "J", "K", "Q" })
            {
                // Her komutandan 2 tane var (toplam 16)
                for (int i = 0; i < 2; i++)
                {
                    deck.Add(new Card
                    {
                        Type = CardType.Commander,
                        Color = color,
                        CommanderName = name,
                        ArmyValue = 0
                    });

                }
            }

            // Ordu kartları: 2'den 10'a (9 değer x 2 renk = 18, x 2 = 36)
            // Her değerden 2 tane var
            for (int value = 2; value <= 10; value++)
            {
                for (int i = 0; i < 2; i++)
                {
                    deck.Add(new Card
                    {
                        Type = CardType.Army,
                        Color = color,
                        ArmyValue = value,
                        CommanderName = null
                    });
                }
            }
        }

        // 2 adet Joker ekle
        deck.Add(new Card
        {
            Type = CardType.Joker,
            Color = CardColor.Red,
            ArmyValue = 0,
            CommanderName = "Joker"
        });

        deck.Add(new Card
        {
            Type = CardType.Joker,
            Color = CardColor.Black,
            ArmyValue = 0,
            CommanderName = "Joker"
        });

        // Trompet kartı ekle (1 adet)
        deck.Add(new Card
        {
            Type = CardType.Trumpet,
            Color = CardColor.None,
            ArmyValue = 0,
            CommanderName = "Trompet"
        });


        Debug.Log($"Deste oluşturuldu: {deck.Count} kart"); // 55 olmalı
        return deck;
    }

    // Desteyi karıştır
    public static void Shuffle(List<Card> deck)
    {
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Card temp = deck[i];
            deck[i] = deck[j];
            deck[j] = temp;
        }
        Debug.Log("Deste karıştırıldı.");
    }

    // Herkese 10'ar kart dağıt
    public static void DealCards(GameState state, List<Card> deck)
    {
        // Her oyuncuya 10 kart ver
        foreach (var player in state.Players)
        {
            for (int i = 0; i < 10; i++)
            {
                // Destenin en üstünden al
                Card card = deck[deck.Count - 1];
                deck.RemoveAt(deck.Count - 1);
                player.Hand.Add(card);
            }
            Debug.Log($"{player.PlayerName} 10 kart aldı.");
        }
    }

    // Kalan kartları ortaya yerleştir (sol ve sağ deste + trompet)
    public static void SetupMiddle(GameState state, List<Card> deck)
    {
        // Trompet kartını bul ve ortaya koy
        Card trumpet = deck.Find(c => c.Type == CardType.Trumpet);
        if (trumpet != null)
        {
            deck.Remove(trumpet);
            state.TrompetPile.Add(trumpet);
            Debug.Log("Trompet ortaya yerleştirildi.");
        }

        // Kalan kartları ikiye böl: sol ve sağ deste
        int half = deck.Count / 2;
        for (int i = 0; i < half; i++)
        {
            state.LeftDeck.Add(deck[i]);
        }
        for (int i = half; i < deck.Count; i++)
        {
            state.RightDeck.Add(deck[i]);
        }

        Debug.Log($"Sol deste: {state.LeftDeck.Count} kart, Sağ deste: {state.RightDeck.Count} kart");
    }
}