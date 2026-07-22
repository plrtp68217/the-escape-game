namespace EscapeGame.Inventory;

/// <summary>
/// Идентификаторы предметов. Используются вместо «магических строк» в коде.
/// В сцене и RPC всё равно хранится/передаётся string, но в C# удобнее
/// обращаться по именованным константам.
/// </summary>
public static class ItemIds
{
	public const string Axe = "axe";
	public const string Health = "health";
	public const string Pill = "pill";
	public const string Syringe = "syringe";
	public const string Key = "key";

	public const string Screwdriver = "screwdriver";
	public const string Crowbar = "crowbar";
	public const string Cutters = "cutters";
	public const string Keycard = "keycard";
	public const string Cipher = "cipher";
	public const string Shovel = "shovel";
	public const string Explosive = "explosive";
}
