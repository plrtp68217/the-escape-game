using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace EscapeGame;

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
        if (!RoundActive
            || !Multiplayer.IsServer()
            || GameState.CurrentPhase != GamePhase.Gameplay)
        {
            return;
        }

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

    // Сервер: заключённый достиг выхода. Если сбежали все — победа заключённых.
    public void NotifyEscaped(long prisonerId)
    {
        if (!Multiplayer.IsServer() || !RoundActive)
        {
            return;
        }

        _escaped.Add(prisonerId);

        int prisonerCount = LobbyManager.Instance.Players.Values
            .Count(p => p.Role == PlayerRole.Prisoner);

        if (prisonerCount > 0 && _escaped.Count >= prisonerCount)
        {
            EndRound(RoundResult.PrisonersWin);
        }
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
