using System.Linq;
using Godot;

namespace EscapeGame.Combat;

/// <summary>
/// Серверный посредник боя и лечения: удары надзирателя, использование
/// расходников, подъём поверженных и синхронизация здоровья. Авторитет
/// сервера. Добавляется как autoload (см. project.godot). По образцу
/// InventoryRelay / InteractionRelay.
/// </summary>
public partial class CombatRelay : Node
{
    public static CombatRelay Instance { get; private set; }

    public override void _Ready()
    {
        Instance = this;
    }

    // Надзиратель бьёт заключённого (клиент прислал цель, сервер проверяет).
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestAttack(long attackerId, long targetId)
    {
        if (!Multiplayer.IsServer())
        {
            return;
        }

        PlayerController attacker = Find(attackerId);
        PlayerController target = Find(targetId);
        if (attacker == null || target == null)
        {
            return;
        }

        if (attacker.Role != PlayerRole.Warden || target.Role != PlayerRole.Prisoner)
        {
            return;
        }
        if (target.VitalState != PlayerVitalState.Alive)
        {
            return;
        }
        if (attacker.Inventory.EquippedSlot?.Item?.Id != G.Door.AxeItemId)
        {
            return;
        }
        // Небольшой запас к дальности — позиции слегка расходятся из-за сети.
        if (attacker.GlobalPosition.DistanceTo(target.GlobalPosition) > G.Combat.AttackRange + 1f)
        {
            return;
        }

        int health = Mathf.Max(0, target.Health - G.Combat.AxeDamage);
        bool downed = health <= 0;
        SyncVitalsTo(target, health, downed ? PlayerVitalState.Downed : PlayerVitalState.Alive);

        // Если это был последний живой заключённый — надзиратель побеждает сразу.
        if (downed)
        {
            RoundManager.Instance?.CheckAllDowned();
        }
    }

    // Игрок использует расходник (аптечка/шприц) на себе, чтобы подлечиться.
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestUseItem(long playerId, int slotIndex)
    {
        if (!Multiplayer.IsServer())
        {
            return;
        }

        PlayerController player = Find(playerId);
        if (player == null || player.VitalState != PlayerVitalState.Alive)
        {
            return;
        }
        if (slotIndex < 0 || slotIndex >= player.Inventory.Slots.Count)
        {
            return;
        }

        Inventory.InventorySlot slot = player.Inventory.Slots[slotIndex];
        if (slot.IsEmpty || !IsMedical(slot.Item.Id))
        {
            return;
        }
        if (player.Health >= G.Combat.MaxHealth)
        {
            return;
        }

        slot.Remove(1);
        Inventory.InventoryRelay.Instance?.BroadcastInventory(player);

        int health = Mathf.Min(G.Combat.MaxHealth, player.Health + G.Combat.HealAmount);
        SyncVitalsTo(player, health, PlayerVitalState.Alive);
    }

    // Заключённый поднимает ближайшего поверженного союзника, расходуя медикамент.
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestRevive(long reviverId)
    {
        if (!Multiplayer.IsServer())
        {
            return;
        }

        PlayerController reviver = Find(reviverId);
        if (reviver == null
            || reviver.Role != PlayerRole.Prisoner
            || reviver.VitalState != PlayerVitalState.Alive)
        {
            return;
        }

        PlayerController target = null;
        float best = G.Combat.ReviveRange * G.Combat.ReviveRange;
        foreach (PlayerController p in PlayerController.AllPlayers.Values)
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

        string medical = FindMedical(reviver.Inventory);
        if (medical == null)
        {
            return;
        }

        reviver.Inventory.RemoveOne(medical);
        Inventory.InventoryRelay.Instance?.BroadcastInventory(reviver);

        SyncVitalsTo(target, G.Combat.ReviveHealth, PlayerVitalState.Alive);
    }

    // Сервер сбрасывает здоровье всех игроков в начале раунда.
    public void ResetAll()
    {
        if (!Multiplayer.IsServer())
        {
            return;
        }

        foreach (PlayerController p in PlayerController.AllPlayers.Values)
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
        return PlayerController.AllPlayers.Values.FirstOrDefault(p => p.PlayerId == playerId);
    }

    private static bool IsMedical(string itemId)
    {
        return itemId == "health" || itemId == "syringe";
    }

    private static string FindMedical(Inventory.PlayerInventory inventory)
    {
        if (inventory.Has("syringe"))
        {
            return "syringe";
        }
        if (inventory.Has("health"))
        {
            return "health";
        }
        return null;
    }
}
