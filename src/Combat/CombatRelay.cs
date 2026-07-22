using System.Linq;
using Godot;
using EscapeGame.GameFlow;
using EscapeGame.Inventory;
using EscapeGame.Player;
using EscapeGame.Services;

namespace EscapeGame.Combat;

/// <summary>
/// Серверный посредник для боя и лечения. Авторитет сервера: клиенты
/// присылают запросы на атаку/лечение/подъём, сервер валидирует и применяет.
/// </summary>
public partial class CombatRelay : Node
{
    public static CombatRelay Instance { get; private set; }

    public override void _Ready()
    {
        Instance = this;
    }

    private bool ValidateSender(long playerId)
    {
        long senderId = Multiplayer.GetRemoteSenderId();
        if (senderId == 0 || playerId == senderId)
        {
            return true;
        }

        GD.PushWarning($"[CombatRelay] PlayerId mismatch: expected {senderId}, got {playerId}");
        return false;
    }

    // Заключённый бьёт топором надзирателя. Клиент присылает id цели,
    // сервер проверяет дистанцию и роли.
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestAttack(long attackerId, long targetId)
    {
        if (!ServiceLocator.Network?.IsServer ?? false || !ValidateSender(attackerId))
        {
            return;
        }

        PlayerController attacker = ServiceLocator.Players.Get(attackerId);
        PlayerController target = ServiceLocator.Players.Get(targetId);

        if (attacker == null
            || target == null
            || attacker.Role != PlayerRole.Prisoner
            || target.Role != PlayerRole.Warden
            || attacker.VitalState != PlayerVitalState.Alive
            || target.VitalState != PlayerVitalState.Alive
            || attacker.Inventory.EquippedSlot?.Item?.Id != ItemIds.Axe)
        {
            return;
        }

        if (attacker.GlobalPosition.DistanceTo(target.GlobalPosition) > G.Combat.AttackRange)
        {
            return;
        }

        int health = System.Math.Max(0, target.Health - G.Combat.AxeDamage);
        PlayerVitalState state = health > 0 ? PlayerVitalState.Alive : PlayerVitalState.Downed;
        SyncVitalsTo(target, health, state);
    }

    // Заключённый удерживает ЛКМ с расходником, чтобы вылечиться.
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestSelfHeal(long playerId)
    {
        if (!ServiceLocator.Network?.IsServer ?? false || !ValidateSender(playerId))
        {
            return;
        }

        PlayerController player = ServiceLocator.Players.Get(playerId);
        if (player == null
            || player.Role != PlayerRole.Prisoner
            || player.VitalState != PlayerVitalState.Alive)
        {
            return;
        }

        InventorySlot slot = player.Inventory.EquippedSlot;
        if (slot == null || slot.IsEmpty || !IsMedical(slot.Item.Id))
        {
            return;
        }

        // RemoveOne обнуляет Item, поэтому запоминаем id ДО удаления.
        string itemId = slot.Item.Id;
        player.Inventory.RemoveOne(itemId);
        InventoryRelay.Instance?.BroadcastInventory(player);

        int healAmount = HealAmountFor(itemId);
        int health = System.Math.Min(G.Combat.MaxHealth, player.Health + healAmount);
        SyncVitalsTo(player, health, PlayerVitalState.Alive);
    }

    // Заключённый удерживает F рядом с поверженным союзником.
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestRevive(long reviverId)
    {
        if (!ServiceLocator.Network?.IsServer ?? false || !ValidateSender(reviverId))
        {
            return;
        }

        PlayerController reviver = ServiceLocator.Players.Get(reviverId);
        if (reviver == null
            || reviver.Role != PlayerRole.Prisoner
            || reviver.VitalState != PlayerVitalState.Alive)
        {
            return;
        }

        PlayerController target = null;
        float best = G.Combat.ReviveRange * G.Combat.ReviveRange;
        foreach (PlayerController p in ServiceLocator.Players.All)
        {
            if (p.Role != PlayerRole.Prisoner || p.VitalState != PlayerVitalState.Downed)
            {
                continue;
            }

            float distance = p.GlobalPosition.DistanceSquaredTo(reviver.GlobalPosition);
            if (distance <= best)
            {
                best = distance;
                target = p;
            }
        }

        if (target == null)
        {
            return;
        }

        SyncVitalsTo(target, G.Combat.ReviveHealth, PlayerVitalState.Alive);
    }

    // Сервер сбрасывает здоровье всех игроков в начале раунда.
    public void ResetAll()
    {
        if (!ServiceLocator.Network?.IsServer ?? false)
        {
            return;
        }

        foreach (PlayerController p in ServiceLocator.Players.All)
        {
            SyncVitalsTo(p, G.Combat.MaxHealth, PlayerVitalState.Alive);
        }
    }

    private void SyncVitalsTo(PlayerController player, int health, PlayerVitalState state)
    {
        Rpc(nameof(SyncVitals), (long)player.PlayerId, health, (int)state);
    }

    // Сервер рассылает всем актуальное здоровье и состояние игрока.
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SyncVitals(long playerId, int health, int state)
    {
        Find(playerId)?.ApplyVitals(health, (PlayerVitalState)state);
    }

    private static PlayerController Find(long playerId)
    {
        return ServiceLocator.Players.All.FirstOrDefault(p => p.PlayerId == playerId);
    }

    private static bool IsMedical(string itemId)
    {
        return itemId == "health" || itemId == "syringe" || itemId == "pill";
    }

    // Сколько лечит расходник данного типа.
    private static int HealAmountFor(string itemId)
    {
        return itemId switch
        {
            "syringe" => G.Combat.SyringeHealAmount,
            "pill" => G.Combat.PillHealAmount,
            _ => G.Combat.HealAmount,
        };
    }
}
