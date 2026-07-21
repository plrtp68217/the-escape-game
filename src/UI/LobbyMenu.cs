using System;
using System.Linq;
using Godot;

namespace EscapeGame.UI;

public partial class LobbyMenu : Control
{
	private Label _playerListLabel;
	private Button _readyButton;
	private Button _startButton;
	private Button _leaveButton;
	private Label _statusLabel;

	private bool _isReady;

	// Локальный IPv4 хоста для шаринга (Веха 10). Считается один раз лениво.
	private string _hostAddress;

	public event Action<bool> ReadyToggled;
	public event Action StartRequested;
	public event Action LeaveRequested;

	public override void _Ready()
	{
		_playerListLabel = GetNode<Label>("VBoxContainer/PlayerListLabel");
		_readyButton = GetNode<Button>("VBoxContainer/ReadyButton");
		_startButton = GetNode<Button>("VBoxContainer/StartButton");
		_leaveButton = GetNode<Button>("VBoxContainer/LeaveButton");
		_statusLabel = GetNode<Label>("VBoxContainer/StatusLabel");

		_readyButton.Pressed += OnReadyPressed;
		_startButton.Pressed += OnStartPressed;
		_leaveButton.Pressed += OnLeavePressed;

		GameState.PhaseChanged += OnPhaseChanged;
		LobbyManager.Instance.LobbyUpdated += Refresh;
		LobbyManager.Instance.GameStarted += OnGameStarted;

		OnPhaseChanged();
		Refresh();
	}

	public override void _ExitTree()
	{
		GameState.PhaseChanged -= OnPhaseChanged;

		if (LobbyManager.Instance != null)
		{
			LobbyManager.Instance.LobbyUpdated -= Refresh;
			LobbyManager.Instance.GameStarted -= OnGameStarted;
		}
	}

	private void OnPhaseChanged()
	{
		if (GameState.CurrentPhase == GamePhase.Lobby)
		{
			Visible = true;
			ResetState();
		}
		else
		{
			Visible = false;
		}
	}

	private void OnReadyPressed()
	{
		_isReady = !_isReady;
		ReadyToggled?.Invoke(_isReady);
		_readyButton.Text = _isReady ? "Не готов" : "Готов";
	}

	private void OnStartPressed()
	{
		StartRequested?.Invoke();
	}

	private void OnGameStarted()
	{
		Visible = false;
	}

	private void OnLeavePressed()
	{
		LeaveRequested?.Invoke();
	}

	private void Refresh()
	{
		var lines = LobbyManager
			.Instance.Players.Values.Select(p =>
				$"{(p.IsReady ? "[Готов] " : "[Ожидание] ")}{p.Name}"
			)
			.ToArray();

		_playerListLabel.Text = lines.Length > 0 ? string.Join("\n", lines) : "Нет игроков";

		if (LobbyManager.Instance.IsHost)
		{
			_startButton.Disabled = false;
			bool canStart = LobbyManager.Instance.CanStartGame();
			string status = canStart
				? "Все готовы. Можно начинать."
				: "Хост может начать игру в любой момент";

			// Подсказываем хосту его адрес — друг в той же сети вводит его в поле IP.
			string ip = GetHostAddress();
			if (!string.IsNullOrEmpty(ip))
			{
				status += $"\nАдрес для друзей: {ip}:{G.Port}";
			}

			_statusLabel.Text = status;
		}
		else
		{
			_startButton.Visible = false;
			_statusLabel.Text = "";
		}
	}

	// Первый подходящий локальный IPv4-адрес хоста: не loopback (127.*) и не
	// link-local (169.254.*). Пустая строка, если такого нет.
	private string GetHostAddress()
	{
		if (_hostAddress != null)
		{
			return _hostAddress;
		}

		_hostAddress = string.Empty;
		foreach (string addr in IP.GetLocalAddresses())
		{
			if (!addr.Contains('.') || addr.Contains(':'))
			{
				continue; // не IPv4
			}
			if (addr.StartsWith("127.") || addr.StartsWith("169.254."))
			{
				continue;
			}

			_hostAddress = addr;
			break;
		}

		return _hostAddress;
	}

	public void ResetState()
	{
		_isReady = false;
		_readyButton.Text = "Готов";
		_startButton.Visible = LobbyManager.Instance.IsHost;
		_statusLabel.Text = "";
		Refresh();
	}
}
