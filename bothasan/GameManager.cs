using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    private GameState _state;
    private List<BotController> _bots = new List<BotController>();
    private TurnManager _turnManager;

    void Start()
    {
        // 1. Oyun durumunu oluştur
        _state = new GameState();

        // 2. Oyuncuları ekle
        _state.Players.Add(new PlayerState(0, "Sen",   false));
        _state.Players.Add(new PlayerState(1, "Bot 1", true));
        _state.Players.Add(new PlayerState(2, "Bot 2", true));
        _state.Players.Add(new PlayerState(3, "Bot 3", true));

        // 3. Desteyi oluştur, karıştır, dağıt
        List<Card> deck = DeckManager.CreateDeck();
        DeckManager.Shuffle(deck);
        DeckManager.DealCards(_state, deck);
        DeckManager.SetupMiddle(_state, deck);

        // 4. Botları oluştur
        //    Bot 1 → MCTS / Hard  (daha stratejik, daha uzun hesaplar)
        //    Bot 2, 3 → Greedy / Easy (hızlı & basit)
        foreach (var player in _state.Players.Where(p => p.IsBot))
        {
            BotController bot = gameObject.AddComponent<BotController>();

            bool         useMcts    = (player.PlayerId == 1);
            AiDifficulty difficulty = (player.PlayerId == 1)
                ? AiDifficulty.Hard
                : AiDifficulty.Easy;

            bot.Initialize(_state, player, useMcts, difficulty);
            _bots.Add(bot);
        }

        // 5. Tur yöneticisini başlat
        _turnManager = gameObject.AddComponent<TurnManager>();
        _turnManager.Initialize(_state, _bots);
        _turnManager.StartGame();
    }
}