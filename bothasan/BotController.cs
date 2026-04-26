// Ana bot dosyası. Botun ne yapacağına bu dosya karar verir.
// Unity'de bir GameObject'e eklenebilir bir component.
// MCTS veya Greedy algoritmasından birini seçerek çalışır.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Botun yapabileceği aksiyonların listesi
public enum BotActionType
{
    DrawFromLeft,       // Sol desteden kart çek
    DrawFromRight,      // Sağ desteden kart çek
    DrawFromSurgun,     // Sürgün destesinden kart çek
    PlaceCommander,     // Komutan koy
    PlaceArmy,          // Orduya kart ekle
    Attack,             // Normal saldır
    SuperAttack,        // Süper saldırı yap
    SendToSurgun        // Ordu kartını sürgüne gönder
}

// Botun aldığı kararı tutan yapı
public class BotDecision
{
    public BotActionType Action;
    public Card CardToPlay;         // Oynayacağı kart (varsa)
    public Cephe SourceCephe;       // Saldıran cephe (varsa)
    public Cephe TargetCephe;       // Hedef cephe (varsa)
    public PlayerState Target;      // Süper saldırı hedefi (varsa)

    // Kararı ekrana yazdır (debug için)
    public override string ToString()
    {
        return $"Bot Kararı: {Action}" +
               (CardToPlay != null ? $" | Kart: {CardToPlay}" : "") +
               (Target     != null ? $" | Hedef: {Target.PlayerName}" : "");
    }
}

public class BotController : MonoBehaviour
{
    private GameState   _gameState;
    private PlayerState _me;

    // MCTS bileşenleri
    private SimpleMcts   _simpleMcts;
    private bool         _useMcts    = true;
    private AiDifficulty _difficulty = AiDifficulty.Normal;

    // Bot'u başlat
    // useMcts=true → SimpleMcts kullan, false → Greedy kullan
    // difficulty → MCTS zorluk seviyesi (Easy / Normal / Hard)
    public void Initialize(
        GameState    gameState,
        PlayerState  myPlayer,
        bool         useMcts    = true,
        AiDifficulty difficulty = AiDifficulty.Normal)
    {
        _gameState  = gameState;
        _me         = myPlayer;
        _useMcts    = useMcts;
        _difficulty = difficulty;

        if (_useMcts)
        {
            _simpleMcts = new SimpleMcts();
            Debug.Log($"{_me.PlayerName} MCTS ile başlatıldı. (Zorluk: {difficulty})");
        }
        else
        {
            Debug.Log($"{_me.PlayerName} Greedy algoritma ile başlatıldı.");
        }
    }

    // PlayerId'yi dışarıya ver (TurnManager için gerekli)
    public int GetPlayerId()
    {
        return _me.PlayerId;
    }

    // Her turda çağrılır, bot ne yapacağına karar verir
    public BotDecision MakeDecision()
    {
        if (_useMcts)
            return MakeDecisionWithMcts();
        else
            return MakeDecisionGreedy();
    }

    // ---- MCTS KARAR ----

    private BotDecision MakeDecisionWithMcts()
    {
        try
        {
            // 1. Hasan'ın state'ini basit formata çevir
            var simpleState = GameStateAdapter.ToSimplifiedState(_gameState, _me.PlayerId);

            // 2. MCTS ile en iyi hamleyi bul
            var settings = new MctsSettings(_difficulty);
            var move     = _simpleMcts.FindBestMove(simpleState, _me.PlayerId, settings);

            // 3. Hamleyi BotDecision'a çevir
            var decision = GameStateAdapter.ToBotDecision(move, _gameState, _me);

            Debug.Log($"{_me.PlayerName} MCTS kararı: {decision.Action}");
            return decision;
        }
        catch (Exception ex)
        {
            Debug.LogError($"{_me.PlayerName} MCTS hatası: {ex.Message} — Greedy'ye geçiliyor.");
            return MakeDecisionGreedy();
        }
    }

    // ---- GREEDY KARAR (yedek) ----

    private BotDecision MakeDecisionGreedy()
    {
        // NOT: Süper saldırı (21 birlik) TurnManager tarafından otomatik tetiklenir.
        // BotController'ın bunu önermesi gerekmez.

        // 1. Normal saldırı yapabilir miyim?
        BotDecision attack = CheckAttack();
        if (attack != null)
        {
            Debug.Log($"{_me.PlayerName}: Saldırıyor! (Greedy)");
            return attack;
        }

        // 3. Mevcut cepheye ordu ekleyebilir miyim?
        BotDecision placeArmy = CheckPlaceArmy();
        if (placeArmy != null)
        {
            Debug.Log($"{_me.PlayerName}: Ordu yerleştiriyor. (Greedy)");
            return placeArmy;
        }

        // 4. Yeni komutan açabilir miyim?
        BotDecision placeCommander = CheckPlaceCommander();
        if (placeCommander != null)
        {
            Debug.Log($"{_me.PlayerName}: Komutan yerleştiriyor. (Greedy)");
            return placeCommander;
        }

        // 5. Hiçbiri değilse kart çek
        BotDecision draw = DecideDraw();
        Debug.Log($"{_me.PlayerName}: Kart çekiyor. (Greedy)");
        return draw;
    }

    // --- KARAR FONKSİYONLARI ---

    // Süper saldırı kontrolü (ordu = tam 21)
    private BotDecision CheckSuperAttack()
    {
        foreach (var cephe in _me.Cepheler)
        {
            if (cephe.CanSuperAttack())
            {
                // En çok kartı olan rakibi hedef al
                PlayerState target = GetStrongestEnemy();
                if (target != null)
                {
                    return new BotDecision
                    {
                        Action      = BotActionType.SuperAttack,
                        SourceCephe = cephe,
                        Target      = target
                    };
                }
            }
        }
        return null;
    }

    // Normal saldırı kontrolü
    private BotDecision CheckAttack()
    {
        foreach (var myCephe in _me.Cepheler)
        {
            if (myCephe.Commander == null) continue;
            if (myCephe.Army.Count == 0) continue; // Ordusu olmayan saldıramaz
            // Joker komutan zayıf saldırı yapamaz
            if (myCephe.Commander.IsJoker) continue;

            List<Cephe> targets = GetAttackableTargets(myCephe.Commander);

            // Saldırabilmek için kendi birliği rakipten fazla olmalı
            Cephe bestTarget = targets
                .Where(t => t.Commander == null || !t.Commander.IsJoker) // Joker komutana saldırılamaz
                .Where(t => myCephe.TotalArmyValueForCombat() > t.TotalArmyValueForCombat())
                .OrderByDescending(t => t.Army.Count)
                .FirstOrDefault();

            if (bestTarget != null)
            {
                return new BotDecision
                {
                    Action      = BotActionType.Attack,
                    SourceCephe = myCephe,
                    TargetCephe = bestTarget
                };
            }
        }
        return null;
    }

    // Ordu yerleştirme kontrolü
    private BotDecision CheckPlaceArmy()
    {
        foreach (var cephe in _me.Cepheler)
        {
            if (cephe.Commander == null) continue;

            int currentTotal = cephe.TotalArmyValueForAdd();
            int lastValue    = cephe.Army.Count > 0
                ? cephe.Army.Last().ArmyValue
                : 0;

            // Uygun ordu kartlarını bul:
            // - Aynı renk
            // - Mevcut son karttan büyük veya eşit
            // - Toplamı 21'i geçmeyecek
            var validCards = _me.Hand
                .Where(c =>
                    // Normal ordu kartı kontrolü
                    (c.Type == CardType.Army &&
                     c.Color == cephe.Commander.Color &&
                     c.ArmyValue >= lastValue &&
                     currentTotal + c.ArmyValue <= 21)
                    ||
                    // Joker kontrolü: renk eşleşmeli ve 21'i tamamlayacak değerde olmalı
                    (c.IsJoker &&
                     c.Color == cephe.Commander.Color &&
                     currentTotal < 21 &&
                     (21 - currentTotal) >= lastValue))
                .OrderBy(c => c.IsJoker ? (21 - currentTotal) : c.ArmyValue)
                .ToList();

            if (validCards.Count > 0)
            {
                // 21'e en yaklaşacak kartı oyna
                Card bestCard = validCards.Last();
                return new BotDecision
                {
                    Action      = BotActionType.PlaceArmy,
                    CardToPlay  = bestCard,
                    SourceCephe = cephe
                };
            }
        }
        return null;
    }

    // Komutan yerleştirme kontrolü
    private BotDecision CheckPlaceCommander()
    {
        // Max 3 cephe açılabilir
        if (_me.Cepheler.Count >= 3) return null;

        // Önce normal komutan koy, yoksa joker kullan
        Card commander = _me.GetCommandersInHand()
            .OrderBy(c => c.IsJoker ? 1 : 0) // Joker son tercih
            .FirstOrDefault();

        if (commander != null)
        {
            return new BotDecision
            {
                Action     = BotActionType.PlaceCommander,
                CardToPlay = commander
            };
        }
        return null;
    }

    // Kart çekme kararı (sol mu, sağ mı, sürgün mü?)
    private BotDecision DecideDraw()
    {
        Card leftCard   = _gameState.LeftTopCard();
        Card rightCard  = _gameState.RightTopCard();
        Card surgunCard = _gameState.TrompetPile.Count > 0
            ? _gameState.TrompetPile[_gameState.TrompetPile.Count - 1]
            : null;

        // Sürgündeki kart yararlıysa oradan çek
        if (surgunCard != null && IsCardUseful(surgunCard))
            return new BotDecision { Action = BotActionType.DrawFromSurgun };

        // Sol yararlıysa soldan çek
        if (leftCard != null && IsCardUseful(leftCard))
            return new BotDecision { Action = BotActionType.DrawFromLeft };

        // Sağ yararlıysa sağdan çek
        if (rightCard != null && IsCardUseful(rightCard))
            return new BotDecision { Action = BotActionType.DrawFromRight };

        // Dolu olan taraftan çek
        if (leftCard  != null) return new BotDecision { Action = BotActionType.DrawFromLeft };
        if (rightCard != null) return new BotDecision { Action = BotActionType.DrawFromRight };
        if (surgunCard != null) return new BotDecision { Action = BotActionType.DrawFromSurgun };

        Debug.Log($"{_me.PlayerName} çekecek kart bulamadı.");
        return new BotDecision { Action = BotActionType.DrawFromLeft };
    }

    // --- YARDIMCI FONKSİYONLAR ---

    // Bir kartın bot için yararlı olup olmadığı
    private bool IsCardUseful(Card card)
    {
        if (card.Type == CardType.Commander) return true;

        // Joker yararlı mı? Mevcut cephelerden birine renk ve değer açısından eklenebiliyorsa evet
        if (card.IsJoker)
        {
            foreach (var cephe in _me.Cepheler)
            {
                if (cephe.Commander == null) continue;
                if (card.Color != cephe.Commander.Color) continue;
                int total = cephe.TotalArmyValueForCombat();
                int last  = cephe.Army.Count > 0 ? cephe.Army.Last().ArmyValue : 0;
                if (total < 21 && (21 - total) >= last)
                    return true;
            }
            return false;
        }

        // Mevcut cephelerden birine eklenebilir mi?
        foreach (var cephe in _me.Cepheler)
        {
            if (cephe.Commander == null) continue;
            if (card.Color == cephe.Commander.Color &&
                card.ArmyValue > (cephe.Army.LastOrDefault()?.ArmyValue ?? 0) &&
                cephe.TotalArmyValueForAdd() + card.ArmyValue <= 21)
                return true;
        }
        return false;
    }

    // En güçlü düşmanı bul (en çok kartı olan)
    private PlayerState GetStrongestEnemy()
    {
        return _gameState.Players
            .Where(p => p.PlayerId != _me.PlayerId)
            .OrderByDescending(p => p.Hand.Count + p.Cepheler.Sum(c => c.Army.Count))
            .FirstOrDefault();
    }

    // Saldırı zinciri: A→J→K→Q→A
    private List<Cephe> GetAttackableTargets(Card myCommander)
    {
        List<Cephe> targets = new List<Cephe>();

        string nextTarget = myCommander.CommanderName switch
        {
            "A" => "J",
            "J" => "K",
            "K" => "Q",
            "Q" => "A",
            _   => ""
        };

        foreach (var player in _gameState.Players)
        {
            if (player.PlayerId == _me.PlayerId) continue;
            foreach (var cephe in player.Cepheler)
            {
                if (cephe.Commander != null &&
                    cephe.Commander.CommanderName == nextTarget)
                    targets.Add(cephe);
            }
        }

        return targets;
    }
}