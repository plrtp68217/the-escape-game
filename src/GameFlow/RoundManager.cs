using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using EscapeGame.Network;
using EscapeGame.Player;
using EscapeGame.Services;

namespace EscapeGame.GameFlow;

/// <summary>
/// Жизненный цикл раунда: таймер и условия победы. Авторитет сервера.
/// Заключённые побеждают, если все сбежали через выход; надзиратель — если
/// вышло время. Добавляется как autoload (см. project.godot).
/// </summary>
public partial class RoundManager : Node
{
    public static RoundManager Instance { get; private set; }

    // Срабатывает на всех пирах, когда раунд завершился.
    public event Action<RoundResult> RoundEnded;

    public int RemainingSeconds { get; private set; }
    public bool RoundActive { get; private set; }

    private double _secondAccumulator;
    private readonly HashSet<long> _escaped = new();

    public override void _Ready()
    {
        Instance = this;

        // Выход игрока посреди раунда может изменить исход (см. OnPeerDisconnected).
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
    }

    // Вызывается на всех пирах при старте игры. Таймер тикает только на сервере.
    public void StartRound()
    {
        _escaped.Clear();
        _secondAccumulator = 0;
        RemainingSeconds = G.Round.Duration;
        RoundActive = true;
    }

    public override void _Process(double delta)
    {
        // Таймер авторитетен на сервере и не зависит от локальной фазы UI: пауза
        // или открытый инвентарь у ХОСТА — это его личный экран, а раунд для
        // остальных игроков продолжается. Единственный гейт — активность раунда.
        if (!RoundActive || !(ServiceLocator.Network?.IsServer ?? false))
        {
            return;
        }

        TickTimer(delta);
        // Подстраховка на случай, если нокаут не был замечен событийно
        // (например, заключённый отключился, оставив живых поверженными).
        CheckAllDowned();
    }

    // Обратный отсчёт раунда (посекундно). По истечении времени — победа надзирателя.
    private void TickTimer(double delta)
    {
        _secondAccumulator += delta;
        if (_secondAccumulator < 1.0)
        {
            return;
        }
        _secondAccumulator -= 1.0;

        RemainingSeconds--;
        Rpc(nameof(SyncTime), RemainingSeconds);

        if (RemainingSeconds <= 0)
        {
            EndRound(RoundResult.WardenWin);
        }
    }

    // Второй путь победы надзирателя: все не сбежавшие заключённые повержены.
    // Грейс-периода нет намеренно — поднять поверженного может только ЖИВОЙ
    // заключённый (см. CombatRelay.RequestRevive), поэтому когда повержены все,
    // состояние необратимо и надзиратель побеждает сразу. Вызывается событийно
    // из CombatRelay при нокауте и как подстраховка каждый кадр.
    public void CheckAllDowned()
    {
        if (!(ServiceLocator.Network?.IsServer ?? false) || !RoundActive)
        {
            return;
        }

        var active = ServiceLocator.Players.All
            .Where(p => p.Role == PlayerRole.Prisoner && !_escaped.Contains(p.PlayerId))
            .ToList();

        bool allDowned = active.Count > 0
            && active.All(p => p.VitalState == PlayerVitalState.Downed);

        if (allDowned)
        {
            EndRound(RoundResult.WardenWin);
        }
    }

    // Сервер: заключённый достиг выхода. Если сбежали все — победа заключённых.
    public void NotifyEscaped(long prisonerId)
    {
        if (!(ServiceLocator.Network?.IsServer ?? false) || !RoundActive)
        {
            return;
        }

        _escaped.Add(prisonerId);
        CheckEscapeComplete();
    }

    // Победа заключённых, когда все ныне числящиеся заключённые сбежали. Счёт
    // берётся из ростера, поэтому вышедшие из игры заключённые не «держат» раунд.
    private void CheckEscapeComplete()
    {
        int prisonerCount = LobbyManager.Instance.Players.Values
            .Count(p => p.Role == PlayerRole.Prisoner);

        if (prisonerCount > 0 && _escaped.Count >= prisonerCount)
        {
            EndRound(RoundResult.PrisonersWin);
        }
    }

    // Игрок отключился. LobbyManager (тоже слушает это событие) убирает его из
    // ростера, а узел игрока освобождается отложенно — поэтому пересчёт исхода
    // откладываем на следующий тик, когда ростер и AllPlayers уже согласованы.
    private void OnPeerDisconnected(long id)
    {
        if (!(ServiceLocator.Network?.IsServer ?? false) || !RoundActive)
        {
            return;
        }

        _escaped.Remove(id);
        CallDeferred(nameof(ReevaluateAfterLeave));
    }

    // Сервер: пересчёт условий победы после выхода игрока из активного раунда.
    private void ReevaluateAfterLeave()
    {
        if (!(ServiceLocator.Network?.IsServer ?? false) || !RoundActive || LobbyManager.Instance == null)
        {
            return;
        }

        var roster = LobbyManager.Instance.Players.Values;
        bool wardenPresent = roster.Any(p => p.Role == PlayerRole.Warden);
        int prisonerCount = roster.Count(p => p.Role == PlayerRole.Prisoner);

        // Надзиратель покинул игру — мешать побегу больше некому: победа заключённых.
        if (!wardenPresent)
        {
            EndRound(RoundResult.PrisonersWin);
            return;
        }

        // Заключённых не осталось — раунд некому выигрывать побегом: победа надзирателя.
        if (prisonerCount == 0)
        {
            EndRound(RoundResult.WardenWin);
            return;
        }

        // Иначе вышел один из заключённых — оставшиеся могли уже все сбежать
        // или быть поверженными.
        CheckEscapeComplete();
        CheckAllDowned();
    }

    private void EndRound(RoundResult result)
    {
        if (!RoundActive)
        {
            return;
        }

        RoundActive = false;
        Rpc(nameof(EndRoundRpc), (int)result);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SyncTime(int seconds)
    {
        RemainingSeconds = seconds;
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void EndRoundRpc(int result)
    {
        RoundActive = false;
        RoundEnded?.Invoke((RoundResult)result);
    }

    public void Reset()
    {
        RoundActive = false;
        RemainingSeconds = 0;
        _escaped.Clear();
    }
}
