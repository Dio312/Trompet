// Basitleştirilmiş MCTS tabanlı karar verici — v3
//
// Yenilikler (v2 → v3):
//   - Joker komutan bilinciği: joker komutanın cephesi saldırı listesine girmez
//   - Cephe bazlı birlik skoru: en gelişmiş cepheye PlaceArmy önceliği verilir
//   - Rakip tehdit algılama: tehdit altında savunmacı hamle tercih edilir
//   - PlaceArmy hedef seçimi: en yüksek birlikli cepheyi önceliklendirir
//
// Süper saldırı (21 birlik) burada ÜRETİLMEZ.
// TurnManager.CheckAndExecuteSuperAttack() her hamlede otomatik tetikler.
// MCTS yalnızca 21'e ulaşmayı teşvik eder.

using System.Collections.Generic;

public class SimpleMcts
{
    private System.Random _rng = new System.Random();

    public SimplifiedMove FindBestMove(
        SimplifiedGameState state,
        int playerId,
        MctsSettings settings)
    {
        var moves = GenerateMoves(state, playerId);

        if (moves.Count == 0)
            return new SimplifiedMove { type = SimplifiedMoveType.DrawLeft };

        if (moves.Count == 1)
            return moves[0];

        // RolloutDepth: her hamleyi kaç kez değerlendireceğimiz (çeşitlilik + kararlılık)
        // Easy → 10, Normal → 20, Hard → 30
        int sampleCount = System.Math.Max(1, settings.RolloutDepth);

        SimplifiedMove bestMove  = moves[0];
        float          bestScore = float.MinValue;

        foreach (var move in moves)
        {
            // Aynı hamleyi sampleCount kez değerlendir, ortalamasını al
            float total = 0f;
            for (int i = 0; i < sampleCount; i++)
                total += EvaluateMove(move, state, playerId);

            float avgScore = total / sampleCount;
            if (avgScore > bestScore)
            {
                bestScore = avgScore;
                bestMove  = move;
            }
        }

        return bestMove;
    }

    // ----------------------------------------------------------------
    //  HAMLE ÜRETİMİ
    // ----------------------------------------------------------------

    private List<SimplifiedMove> GenerateMoves(SimplifiedGameState state, int playerId)
    {
        var moves  = new List<SimplifiedMove>();
        var player = FindPlayer(state, playerId);

        if (player == null)
        {
            moves.Add(new SimplifiedMove { type = SimplifiedMoveType.DrawLeft });
            return moves;
        }

        // 1. Kart çekme
        if (state.leftDeckCount  > 0)
            moves.Add(new SimplifiedMove { type = SimplifiedMoveType.DrawLeft });
        if (state.rightDeckCount > 0)
            moves.Add(new SimplifiedMove { type = SimplifiedMoveType.DrawRight });
        if (state.exileCount > 0)
            moves.Add(new SimplifiedMove { type = SimplifiedMoveType.DrawSurgun });

        // 2. Komutan koyma — elde komutan/joker VAR ve max 3 cephe açılmamışsa
        if (player.frontCount < 3 && player.hasCommanderInHand)
            moves.Add(new SimplifiedMove { type = SimplifiedMoveType.PlaceCommander });

        // 3. Ordu koyma — cephesi açık, ordu eklenebilir ve elde kart var
        //    Sadece joker komutan OLMAYAN cephelere öneri yap (joker kural: saldırı yapamaz,
        //    ordusunu geliştirmenin faydası daha düşük — yine de geliştirilebilir)
        if (player.canPlaceArmy && player.handCount > 0)
        {
            for (int i = 0; i < player.fronts.Count; i++)
            {
                var front = player.fronts[i];
                if (front.commanderPresent && front.armyValue < 21)
                {
                    moves.Add(new SimplifiedMove
                    {
                        type             = SimplifiedMoveType.PlaceArmy,
                        targetFrontIndex = i
                    });
                }
            }
        }

        // 4. Saldırı — kendi cephesi orduya sahip VE joker komutan DEĞİLSE
        // NOT: Saldırı zinciri (A→J→K→Q→A) TurnManager.Attack() tarafından uygulanır.
        // MCTS yalnızca joker komutan kurallarını filtreler.
        if (player.hasOpenFront)
        {
            for (int myIdx = 0; myIdx < player.fronts.Count; myIdx++)
            {
                var myFront = player.fronts[myIdx];

                // Joker komutan zayıf saldırı yapamaz
                if (myFront.isJokerCommander) continue;

                // Ordu yoksa saldırılamaz
                if (!myFront.hasArmyCards) continue;

                foreach (var opp in state.players)
                {
                    if (opp.playerId == playerId) continue;

                    for (int oppIdx = 0; oppIdx < opp.fronts.Count; oppIdx++)
                    {
                        var oppFront = opp.fronts[oppIdx];

                        // Joker komutanın cephesine zayıf saldırı yapılamaz
                        if (oppFront.isJokerCommander) continue;

                        moves.Add(new SimplifiedMove
                        {
                            type             = SimplifiedMoveType.Attack,
                            sourceFrontIndex = myIdx,
                            targetPlayerId   = opp.playerId,
                            targetFrontIndex = oppIdx
                        });
                    }
                }
            }
        }

        return moves;
    }

    // ----------------------------------------------------------------
    //  HAMLE PUANLAMA
    // ----------------------------------------------------------------

    private float EvaluateMove(SimplifiedMove move, SimplifiedGameState state, int playerId)
    {
        var player = FindPlayer(state, playerId);
        if (player == null) return 0f;

        float score = BaseScore(move, state, player);

        // El büyükse oynamak daha değerli (el boşaltma teşviki)
        score -= player.handCount * 0.4f;

        // Birlik gücü bonusu (küçük)
        score += player.bestFrontUnits * 0.06f;

        // Tehdit altındaysa savunmacı davranış: ordu geliştirmeye ek bonus
        if (player.isUnderThreat && move.type == SimplifiedMoveType.PlaceArmy)
            score += 3f;

        // Hafif rastgelelik — eşit puanlarda çeşitlilik
        score += (float)(_rng.NextDouble() * 0.05);

        return score;
    }

    private float BaseScore(SimplifiedMove move, SimplifiedGameState state, SimplifiedPlayerState player)
    {
        switch (move.type)
        {
            case SimplifiedMoveType.Attack:
                return ScoreAttack(move, state, player);

            case SimplifiedMoveType.PlaceArmy:
                return ScorePlaceArmy(move, player);

            case SimplifiedMoveType.PlaceCommander:
                return ScorePlaceCommander(player);

            case SimplifiedMoveType.DrawSurgun:
                return 4f;   // Bilinen kart avantajı

            case SimplifiedMoveType.DrawLeft:
            case SimplifiedMoveType.DrawRight:
                return 3f;   // En düşük öncelik

            default:
                return 0f;
        }
    }

    private float ScoreAttack(SimplifiedMove move, SimplifiedGameState state, SimplifiedPlayerState player)
    {
        float score = 10f;

        // Hedef cephe bilgisini al
        if (move.targetPlayerId >= 0 && move.targetPlayerId < state.players.Count)
        {
            var opp = FindPlayer(state, move.targetPlayerId);
            if (opp != null)
            {
                // Rakibin eli azsa saldırı daha acil
                if (opp.handCount <= 3) score += 5f;
                if (opp.handCount <= 1) score += 4f;

                // Hedef cephenin birlik değerini al — kendi gücümüze göre saldırı değeri
                if (move.targetFrontIndex >= 0 && move.targetFrontIndex < opp.fronts.Count)
                {
                    var tgtFront = opp.fronts[move.targetFrontIndex];
                    // Hedef cephede birlik fazlaysa yok etmek daha değerli
                    score += tgtFront.armyCount * 0.5f;
                }
            }
        }

        // Saldıran cephenin 21'e yakınlığı — yakınsa süper saldırıya giden önce saldır
        if (move.sourceFrontIndex >= 0 && move.sourceFrontIndex < player.fronts.Count)
        {
            var srcFront = player.fronts[move.sourceFrontIndex];
            if (srcFront.armyValue >= 18) score += 2f; // Süper saldırı basamağı
        }

        return score;
    }

    private float ScorePlaceArmy(SimplifiedMove move, SimplifiedPlayerState player)
    {
        float score = 8f;

        // Hedef cephenin mevcut birlik gücüne göre bonus
        if (move.targetFrontIndex >= 0 && move.targetFrontIndex < player.fronts.Count)
        {
            var front = player.fronts[move.targetFrontIndex];

            // En güçlü cepheyi daha da güçlendir (21'e yaklaştır)
            if (front.armyValue >= 18) score += 6f;  // Süper saldırıya 1-3 kart kaldı!
            else if (front.armyValue >= 15) score += 4f;
            else if (front.armyValue >= 10) score += 2f;

            // Joker komutanın cephesine ordu koymanın stratejik değeri daha düşük
            // (zayıf saldırı yapamaz, ama süper saldırı yapabilir)
            if (front.isJokerCommander) score -= 1f;
        }

        return score;
    }

    private float ScorePlaceCommander(SimplifiedPlayerState player)
    {
        // İlk cephe açmak kritik, sonrakiler azalan önem
        return player.frontCount switch
        {
            0 => 9f,
            1 => 6f,
            2 => 4f,
            _ => 1f
        };
    }

    // ----------------------------------------------------------------
    //  YARDIMCI
    // ----------------------------------------------------------------

    private static SimplifiedPlayerState FindPlayer(SimplifiedGameState state, int playerId)
    {
        foreach (var p in state.players)
            if (p.playerId == playerId) return p;
        return null;
    }
}
