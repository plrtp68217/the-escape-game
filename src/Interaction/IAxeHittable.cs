using EscapeGame.Player;

namespace EscapeGame.Interaction;

/// <summary>
/// Объект, по которому можно ударить топором. Реализуется дверьми, ящиками
/// и другими препятствиями, выбиваемыми заключённым.
/// </summary>
public interface IAxeHittable
{
	void HitWithAxe(PlayerController player);
}
