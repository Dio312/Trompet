// Bir cepheyi temsil eder (bir komutan + onun ordusu)

using System.Collections.Generic;
using System.Linq;

public class Cephe
{
    public Card Commander { get; set; }           // Bu cephenin komutanı
    public List<Card> Army { get; set; }          // Komutanın ordusu (küçükten büyüğe)

    public Cephe()
    {
        Army = new List<Card>();
    }

    // Ordunun toplam değerini hesaplar
    // UYARI: Bu metod Joker kartlarını 0 olarak sayar ve yanlış sonuç verir!
    // Bunun yerine TotalArmyValueForCombat() veya TotalArmyValueForAdd() kullanın.
    [System.Obsolete("Bu metod Joker kartlarını yanlış hesaplar. TotalArmyValueForCombat() veya TotalArmyValueForAdd() kullanın.")]
    public int TotalArmyValue()
    {
        return Army.Sum(card => card.ArmyValue);
    }

    // Joker'in bulunduğu indeksi bul, yoksa -1 döner
    public int GetJokerIndex()
    {
        for (int i = 0; i < Army.Count; i++)
            if (Army[i].IsJoker) return i;
        return -1;
    }

    // Joker'in alabileceği minimum değeri hesapla
    public int GetJokerMin()
    {
        int jokerIndex = GetJokerIndex();
        if (jokerIndex < 0) return 0;
        return jokerIndex > 0 ? Army[jokerIndex - 1].ArmyValue : 2;
    }

    // Joker'in alabileceği maksimum değeri hesapla
    public int GetJokerMax()
    {
        int jokerIndex = GetJokerIndex();
        if (jokerIndex < 0) return 0;
        return jokerIndex < Army.Count - 1 ? Army[jokerIndex + 1].ArmyValue : 10;
    }

    // Saldırı ve savunmada kullanılan birlik değeri (joker max değeriyle)
    public int TotalArmyValueForCombat()
    {
        int total = 0;
        foreach (var card in Army)
        {
            if (card.IsJoker)
                total += GetJokerMax();
            else
                total += card.ArmyValue;
        }
        return total;
    }

    // Yeni kart eklenirken kontrol için kullanılan birlik değeri (joker min değeriyle)
    public int TotalArmyValueForAdd()
    {
        int total = 0;
        foreach (var card in Army)
        {
            if (card.IsJoker)
                total += GetJokerMin();
            else
                total += card.ArmyValue;
        }
        return total;
    }

    // Bu cephe süper saldırı yapabilir mi? (tam 21)
    public bool CanSuperAttack()
    {
        return TotalArmyValueForCombat() == 21;
    }
        // Cephenin geçerli olup olmadığını kontrol eder
    public bool IsValid()
    {
        // Commander null olmamalı
        if (Commander == null)
            return false;

        // Army null olmamalı
        if (Army == null)
            return false;

        // Ordu kartları küçükten büyüğe sıralı olmalı (Joker hariç)
        for (int i = 1; i < Army.Count; i++)
        {
            if (!Army[i].IsJoker && !Army[i - 1].IsJoker)
            {
                if (Army[i].ArmyValue < Army[i - 1].ArmyValue)
                    return false;
            }
        }

        return true;
    }

}
