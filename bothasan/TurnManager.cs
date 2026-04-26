using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    private GameState _state;
    private List<BotController> _bots;
    private bool _middleEmptied = false;
    private string _winCondition = "";

    // Dışarıdan başlatmak için
    public void Initialize(GameState state, List<BotController> bots)
    {
        _state = state;
        _bots = bots;
    }

    // Oyun döngüsünü başlat
    public void StartGame()
    {
        StartCoroutine(GameLoop());
    }

    // Ana oyun döngüsü — her tur sırayla çalışır
    private IEnumerator GameLoop()
    {
        Debug.Log("=== OYUN BAŞLADI ===");

        while (!IsGameOver())
        {
            PlayerState current = _state.CurrentPlayer();
            Debug.Log($"\n>>> {current.PlayerName}'in sırası");

            if (current.IsBot)
            {
                yield return new WaitForSeconds(1f);
                BotController bot = GetBot(current.PlayerId);

                // Süper saldırı önce otomatik kontrol edilir (hamle harcamaz)
                CheckAndExecuteSuperAttack(current);

                // Ardından normal hamle yapılır
                yield return new WaitForSeconds(0.5f);
                BotDecision decision = bot.MakeDecision();
                Debug.Log(decision.ToString());
                ApplyDecision(decision, current);
            }
            else
            {
                // İnsan sırası: şimdilik otomatik kart çekiyor
                // İleride buraya UI bağlanacak
                Debug.Log("İnsan oyuncunun sırası (henüz UI yok, kart çekiyor)");
                DrawCard(current, fromLeft: true);
            }

            // Oyun bitti mi kontrol et
            if (IsGameOver()) break;

            // Sırayı bir sonrakine geçir
            _state.NextTurn();

            // Çok hızlı dönmesin diye küçük bekleme
            yield return new WaitForSeconds(0.2f);
        }

        Debug.Log("=== OYUN BİTTİ ===");
        PrintWinner();
    }

    // Kararı gerçekten uygula
    private void ApplyDecision(BotDecision decision, PlayerState player)
    {
        switch (decision.Action)
        {
            case BotActionType.DrawFromLeft:
                DrawCard(player, fromLeft: true);
                break;

            case BotActionType.DrawFromRight:
                DrawCard(player, fromLeft: false);
                break;

            case BotActionType.DrawFromSurgun:
                DrawCard(player, fromLeft: false, fromSurgun: true);
                break;

            case BotActionType.PlaceCommander:
                PlaceCommander(player, decision.CardToPlay);
                break;

            case BotActionType.PlaceArmy:
                PlaceArmy(player, decision.CardToPlay, decision.SourceCephe);
                break;

            case BotActionType.SendToSurgun:
                SendToSurgun(player, decision.CardToPlay, decision.SourceCephe);
                break;

            case BotActionType.Attack:
                Attack(decision.SourceCephe, decision.TargetCephe, player);
                break;

            case BotActionType.SuperAttack:
                // Bot manuel süper saldırı kararı verirse bu case artık kullanılmaz.
                // Süper saldırı yalnızca CheckAndExecuteSuperAttack ile otomatik tetiklenir.
                // Bu case kasıtlı olarak boş bırakıldı.
                break;
        }

        // Her hamle sonrası süper saldırı kontrolü yap (çift tetikleme önlendi)
        if (decision.Action != BotActionType.SuperAttack)
            CheckAndExecuteSuperAttack(player);
    }

    // --- HAMLE FONKSİYONLARI ---

    // Kart çek (sol, sağ veya sürgünden)
    private void DrawCard(PlayerState player, bool fromLeft, bool fromSurgun = false)
{
    if (fromSurgun)
    {
        if (_state.TrompetPile.Count == 0)
        {
            Debug.Log($"{player.PlayerName} sürgünden çekemedi, sürgün boş.");
            return;
        }
        Card card = _state.TrompetPile[_state.TrompetPile.Count - 1];
        _state.TrompetPile.RemoveAt(_state.TrompetPile.Count - 1);
        player.Hand.Add(card);
        Debug.Log($"{player.PlayerName} sürgünden çekti: {card}");
        return;
    }

    List<Card> deck = fromLeft ? _state.LeftDeck : _state.RightDeck;
    string side = fromLeft ? "soldan" : "sağdan";

    if (deck.Count == 0)
    {
        List<Card> otherDeck = fromLeft ? _state.RightDeck : _state.LeftDeck;
        string otherSide = fromLeft ? "sağdan" : "soldan";

        if (otherDeck.Count == 0)
        {
            Debug.Log($"{player.PlayerName} kart çekemedi, her iki deste de boş.");
            return;
        }

        Debug.Log($"{player.PlayerName} {side} boş, {otherSide} çekiyor.");
        deck = otherDeck;
        side = otherSide;
    }

    Card drawnCard = deck[deck.Count - 1];
    deck.RemoveAt(deck.Count - 1);
    player.Hand.Add(drawnCard);
    Debug.Log($"{player.PlayerName} {side} çekti: {drawnCard}");
}

    // Komutan yerleştir
    private void PlaceCommander(PlayerState player, Card commander)
{
    if (player.Cepheler.Count >= 3)
    {
        Debug.Log($"{player.PlayerName} max 3 cepheye ulaştı, komutan koyamaz.");
        return;
    }

    player.Hand.Remove(commander);
    Cephe yeniCephe = new Cephe();
    yeniCephe.Commander = commander;
    player.Cepheler.Add(yeniCephe);

    Debug.Log($"{player.PlayerName} → Yeni cephe açtı: " +
              $"Komutan {commander.CommanderName} ({commander.Color}). " +
              $"Toplam cephe sayısı: {player.Cepheler.Count}");
}

    // Orduya kart ekle
    private void PlaceArmy(PlayerState player, Card armyCard, Cephe cephe)
{
    // Önce elden çıkar ve Army'ye ekle, sonra hesapla
    player.Hand.Remove(armyCard);
    cephe.Army.Add(armyCard);

    // Joker Army'ye eklendikten sonra doğru aralık hesaplanır
    // ArmyValue = 0 yalnızca joker için yapılır
    if (armyCard.IsJoker)
    {
        // Joker'in ArmyValue zaten 0, tekrar atamaya gerek yok
        Debug.Log($"    {armyCard.Color} Joker cepheye eklendi. " +
                  $"Aralık: {cephe.GetJokerMin()} - {cephe.GetJokerMax()} " +
                  $"Savaş değeri: {cephe.GetJokerMax()}");
    }

    // Ordunun şu anki dizilimini göster
    string armyList = "";
    foreach (var card in cephe.Army)
    {
        if (card.IsJoker)
            armyList += $"{cephe.GetJokerMax()}(J-{card.Color}) ";
        else
            armyList += card.ArmyValue + " ";
    }

    Debug.Log($"{player.PlayerName} → Komutan {cephe.Commander.CommanderName} " +
          $"({cephe.Commander.Color}) cephesine {armyCard} " +
          $"ordu kartı koydu. " +
          $"\n    {cephe.Commander.CommanderName} ({cephe.Commander.Color}) cephe dizilimi: " +
          $"[{armyList.Trim()}] → Toplam: {cephe.TotalArmyValueForCombat()}");
}

    // Normal saldırı (zayıf saldırı)
    private void Attack(Cephe attackerCephe, Cephe defenderCephe, PlayerState attacker)
{
    PlayerState defender = FindPlayerByCephe(defenderCephe);
    string defenderName = defender != null ? defender.PlayerName : "Bilinmeyen";

    // Joker komutan kuralı: Joker komutanın cephesine zayıf saldırı yapılamaz
    if (defenderCephe.Commander != null && defenderCephe.Commander.IsJoker)
    {
        Debug.Log($"{attacker.PlayerName} saldıramadı: " +
                  $"{defenderName}'in cephesi Joker komutana sahip, " +
                  $"zayıf saldırı yapılamaz.");
        return;
    }

    // Joker komutan kuralı: Joker komutanın bulunduğu cephe zayıf saldırı yapamaz
    if (attackerCephe.Commander != null && attackerCephe.Commander.IsJoker)
    {
        Debug.Log($"{attacker.PlayerName} saldıramadı: " +
                  $"Kendi cephesi Joker komutana sahip, zayıf saldırı yapamaz.");
        return;
    }

    // Saldırı ve savunmada joker max değeriyle hesaplanır
    int attackerPower = attackerCephe.TotalArmyValueForCombat();
    int defenderPower = defenderCephe.TotalArmyValueForCombat();

    if (attackerPower <= defenderPower)
    {
        Debug.Log($"{attacker.PlayerName} saldıramadı: " +
                  $"Birlik sayısı ({attackerPower}) rakipten ({defenderPower}) fazla değil.");
        return;
    }

    Debug.Log($"{attacker.PlayerName} → ZAYIF SALDIRI!" +
              $"\n    Saldıran: Komutan {attackerCephe.Commander.CommanderName} " +
              $"({attackerCephe.Commander.Color}) — Birlik: {attackerPower}" +
              $"\n    Hedef: {defenderName}'in Komutan {defenderCephe.Commander?.CommanderName} " +
              $"cephesi — Birlik: {defenderPower}");

    // Saldıran sadece son ordu kartını kaybeder
    if (attackerCephe.Army.Count > 0)
    {
        Card lostCard = attackerCephe.Army[attackerCephe.Army.Count - 1];
        attackerCephe.Army.RemoveAt(attackerCephe.Army.Count - 1);
        Debug.Log($"    {attacker.PlayerName} son ordu kartını kaybetti: {lostCard}");
    }

    // Saldırılan cephe tüm orduyu kaybeder
    if (defenderCephe.Army.Count > 0)
    {
        int lostCount = defenderCephe.Army.Count;
        defenderCephe.Army.Clear();
        Debug.Log($"    {defenderName}'in {lostCount} ordu kartı yok edildi.");

        // Kaybedilen kart sayısı kadar hemen çek
        for (int i = 0; i < lostCount; i++)
            DrawCardFromMiddle(defender);
    }
    else if (defenderCephe.Commander != null)
    {
        // Ordu yoksa komutan kaybedilir, cephe yok olur
        Debug.Log($"    {defenderName}'in Komutan {defenderCephe.Commander.CommanderName} " +
                  $"cephesi yok edildi!");
        defender.Cepheler.Remove(defenderCephe);

        // 1 kart çeker
        DrawCardFromMiddle(defender);
    }
}

    // Süper saldırı (ordu = 21)
    private void SuperAttack(Cephe attackerCephe, PlayerState target, PlayerState attacker)
    {
        Debug.Log($"{attacker.PlayerName} → SÜPER SALDIRI!" +
              $"\n    Saldıran: Komutan {attackerCephe.Commander.CommanderName} " +
              $"({attackerCephe.Commander.Color}) — Birlik: {attackerCephe.TotalArmyValueForCombat()}");

        if (target.Cepheler.Count == 0)
        {
            Debug.Log($"    {target.PlayerName}'in cephesi yok, saldırı yapılamadı.");
            return;
    }

    // En çok birliği olan cepheyi hedef al
    Cephe hedefCephe = target.Cepheler
        .OrderByDescending(c => c.Army.Count)
        .First();

    string hedefKomutan = hedefCephe.Commander != null
        ? $"Komutan {hedefCephe.Commander.CommanderName} ({hedefCephe.Commander.Color})"
        : "Komutansız cephe";

    int destroyedCount = hedefCephe.Army.Count + (hedefCephe.Commander != null ? 1 : 0);
    target.Cepheler.Remove(hedefCephe);

    Debug.Log($"    {target.PlayerName}'in {hedefKomutan} cephesi tamamen yok edildi! " +
              $"({destroyedCount} kart)");

    // Saldıran cephe tüm ordusunu kaybeder, komutanı kalır
    attackerCephe.Army.Clear();
    Debug.Log($"    {attacker.PlayerName}'in ordusu sıfırlandı, komutanı kaldı.");

    // Saldırıya uğrayan oyuncu hemen çeker
    for (int i = 0; i < destroyedCount; i++)
        DrawCardFromMiddle(target);
}

    // Ortadaki destelerden kart çek (saldırı sonrası)
    private void DrawCardFromMiddle(PlayerState player)
    {
        // Önce sol, yoksa sağ, yoksa trompet
        if (_state.LeftDeck.Count > 0)
        {
            DrawCard(player, fromLeft: true);
        }
        else if (_state.RightDeck.Count > 0)
        {
            DrawCard(player, fromLeft: false);
        }
        else if (_state.TrompetPile.Count > 0)
        {
            Card card = _state.TrompetPile[_state.TrompetPile.Count - 1];
            _state.TrompetPile.RemoveAt(_state.TrompetPile.Count - 1);
            player.Hand.Add(card);
            Debug.Log($"{player.PlayerName} trompetten kart çekti: {card}");
        }
    }

    // --- YARDIMCI FONKSİYONLAR ---

    // Oyun bitti mi?
    private bool IsGameOver()
    {
        // Eli biten kazanır
        foreach (var player in _state.Players)
        {
            if (player.Hand.Count == 0)
            {
                Debug.Log($"{player.PlayerName} elindeki tüm kartları bitirdi!");
                _middleEmptied = false;
                _winCondition = "hand_empty";
                return true;
            }
        }

        // Sol ve sağ deste boşaldıysa ve trompet çekildiyse
        bool decksEmpty = _state.LeftDeck.Count == 0 && _state.RightDeck.Count == 0;
        bool trompetCekildi = _state.TrompetPile.Count == 0;

        if (decksEmpty && trompetCekildi)
        {
            Debug.Log("Sol ve sağ deste boşaldı, trompet çekildi. Oyun bitiyor.");
            _middleEmptied = true;
            _winCondition = "middle_empty";
            return true;
        }

        return false;
    }

    // Kazananı bul ve yazdır
    private void PrintWinner()
    {
        PlayerState winner = null;

        if (_middleEmptied)
        {
            // Trompet yöntemi: en yüksek birlik toplamı kazanır
            int maxBirlik = -1;
            foreach (var player in _state.Players)
            {
                int total = player.Cepheler.Sum(c => c.TotalArmyValueForCombat());
                Debug.Log($"    {player.PlayerName} toplam birlik: {total}");
                if (total > maxBirlik)
                {
                    maxBirlik = total;
                    winner = player;
                }
            }
            Debug.Log($"🏆 KAZANAN: {winner.PlayerName}");
            Debug.Log($"🎺 Kazanma yöntemi: Trompet — En yüksek birlik sayısı: {maxBirlik}");
        }
        else if (_winCondition == "hand_empty")
        {
            // El bitirme: elini ilk boşaltan kazanır
            foreach (var player in _state.Players)
            {
                if (player.Hand.Count == 0)
                {
                    winner = player;
                    break;
                }
            }
            Debug.Log($"🏆 KAZANAN: {winner.PlayerName}");
            Debug.Log($"🃏 Kazanma yöntemi: Elini Bitirme — Elindeki tüm kartları oynayarak kazandı.");
        }
    }

    // Ordu 21'e ulaştıysa anında süper saldırı uygula
    private void CheckAndExecuteSuperAttack(PlayerState player)
    {
        foreach (var cephe in player.Cepheler)
        {
            // Süper saldırı kontrolünde joker max değeriyle hesaplanır
            if (cephe.TotalArmyValueForCombat() == 21)
            {
                Debug.Log($"{player.PlayerName}'in {cephe.Commander?.CommanderName} " +
                          $"cephesi 21'e ulaştı! Süper saldırı zorunlu!");

                // En çok birliği olan rakip cepheyi bul
                PlayerState target = null;
                Cephe targetCephe = null;
                int maxArmy = -1;

                foreach (var enemy in _state.Players)
                {
                    if (enemy.PlayerId == player.PlayerId) continue;
                    foreach (var enemyCephe in enemy.Cepheler)
                    {
                        int total = enemyCephe.Army.Count;
                        if (total > maxArmy)
                        {
                            maxArmy = total;
                            target = enemy;
                            targetCephe = enemyCephe;
                        }
                    }
                }

                if (target != null)
                    SuperAttack(cephe, target, player);
                else
                    Debug.Log($"    Saldırılacak rakip cephe bulunamadı.");

                break;
            }
        }
    }

    // Sürgüne kart gönder
    private void SendToSurgun(PlayerState player, Card card, Cephe cephe)
    {
        // Sadece cepheye en son eklenen kart sürgüne gönderilebilir (LIFO)
        if (cephe.Army.Count == 0)
        {
            Debug.Log($"{player.PlayerName} sürgüne gönderemedi: Cephede ordu yok.");
            return;
        }

        Card lastCard = cephe.Army[cephe.Army.Count - 1];
        if (lastCard != card)
        {
            Debug.Log($"{player.PlayerName} sürgüne gönderemedi: " +
                      $"Sadece en son eklenen kart sürgüne gönderilebilir.");
            return;
        }

        cephe.Army.RemoveAt(cephe.Army.Count - 1);
        _state.TrompetPile.Add(card);
        Debug.Log($"{player.PlayerName} sürgüne gönderdi: {card} " +
                  $"(Komutan {cephe.Commander?.CommanderName} cephesinden)");
    }

    // Bir cephenin hangi oyuncuya ait olduğunu bul
    private PlayerState FindPlayerByCephe(Cephe cephe)
    {
        foreach (var player in _state.Players)
        {
            if (player.Cepheler.Contains(cephe))
                return player;
        }
        return null;
    }

    // PlayerId'ye göre botu bul
    private BotController GetBot(int playerId)
    {
        foreach (var bot in _bots)
        {
            if (bot.GetPlayerId() == playerId)
                return bot;
        }
        return null;
    }
}