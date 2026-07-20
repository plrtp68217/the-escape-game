using Godot;

namespace EscapeGame.UI;

/// <summary>
/// Главное меню: ввод имени/IP, кнопки Host/Join и статусное сообщение.
/// </summary>
public partial class MainMenu : Control
{
	private LineEdit _nameInput;
	private LineEdit _ipInput;
	private Label _statusLabel;

	public event System.Action HostRequested;
	public event System.Action<string> JoinRequested;
	public event System.Action SettingsRequested;

	public string PlayerName
	{
		get
		{
			string name = _nameInput.Text?.Trim() ?? string.Empty;
			return string.IsNullOrWhiteSpace(name) ? G.Messages.DefaultPlayerName : name;
		}
	}

	public string Address
	{
		get
		{
			string address = _ipInput.Text?.Trim() ?? string.Empty;
			return string.IsNullOrWhiteSpace(address) ? G.DefaultAddress : address;
		}
	}

	public override void _Ready()
	{
		_nameInput = GetNode<LineEdit>("VBoxContainer/NameInput");
		_ipInput = GetNode<LineEdit>("VBoxContainer/IpInput");
		_statusLabel = GetNode<Label>("VBoxContainer/StatusLabel");

		GetNode<Button>("VBoxContainer/HostButton").Pressed += () => HostRequested?.Invoke();
		GetNode<Button>("VBoxContainer/JoinButton").Pressed += () => JoinRequested?.Invoke(Address);
		GetNode<Button>("VBoxContainer/SettingsButton").Pressed += () => SettingsRequested?.Invoke();
	}

	public void SetStatus(string text)
	{
		_statusLabel.Text = text;
	}
}
