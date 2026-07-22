using System;
using Godot;
using EscapeGame.GameFlow;
using EscapeGame.Network;

namespace EscapeGame.Tests;

/// <summary>
/// Headless-тест лобби-потока: хост → имя → готовность → старт → Gameplay.
/// Запускается через Godot CLI с --headless. Exit code 0 — успех, 1 — ошибка.
/// </summary>
public partial class LobbyFlowTest : Node
{
	private enum TestStage
	{
		WaitForMain,
		CreateHost,
		SetName,
		SetReady,
		StartGame,
		VerifyGameplay,
		Done,
	}

	private TestStage _stage = TestStage.WaitForMain;
	private double _waitTime;
	private bool _failed;

	public override void _Ready()
	{
		GD.Print("[LobbyFlowTest] Запуск headless-теста");
	}

	private bool IsMainReady()
	{
		return GetParent()?.GetNodeOrNull("GameFlow") != null;
	}

	public override void _Process(double delta)
	{
		if (_failed || _stage == TestStage.Done)
		{
			return;
		}

		_waitTime += delta;
		if (_waitTime < 0.5)
		{
			return;
		}

		_waitTime = 0;
		Progress();
	}

	private void Progress()
	{
		if (_stage == TestStage.WaitForMain)
		{
			if (IsMainReady())
			{
				GD.Print("[LobbyFlowTest] Main и GameFlow готовы");
				_stage = TestStage.CreateHost;
			}
			return;
		}

		if (_stage == TestStage.CreateHost)
		{
			GD.Print("[LobbyFlowTest] Создание хоста");
			NetworkManager.Instance.CreateHost();
			_stage = TestStage.SetName;
			return;
		}

		if (_stage == TestStage.SetName)
		{
			GD.Print("[LobbyFlowTest] Установка имени");
			LobbyManager.Instance.SendPlayerName("TestHost");
			_stage = TestStage.SetReady;
			return;
		}

		if (_stage == TestStage.SetReady)
		{
			GD.Print("[LobbyFlowTest] Переключение готовности");
			LobbyManager.Instance.SendReady(true);
			_stage = TestStage.StartGame;
			return;
		}

		if (_stage == TestStage.StartGame)
		{
			GD.Print("[LobbyFlowTest] Форсированный старт игры");
			LobbyManager.Instance.StartGame();
			_stage = TestStage.VerifyGameplay;
			return;
		}

		if (_stage == TestStage.VerifyGameplay)
		{
			if (GameState.CurrentPhase == GamePhase.Gameplay)
			{
				GD.Print("[LobbyFlowTest] OK: фаза GamePhase.Gameplay достигнута");
				_stage = TestStage.Done;
				Shutdown(0);
			}
			else
			{
				GD.PrintErr($"[LobbyFlowTest] FAIL: ожидалась фаза Gameplay, получена {GameState.CurrentPhase}");
				_failed = true;
				Shutdown(1);
			}
		}
	}

	private void Shutdown(int exitCode)
	{
		NetworkManager.Instance?.Disconnect();
		GD.Print($"[LobbyFlowTest] Завершение с exit code {exitCode}");

		// Даём stdout/stderr успеть сброситься до выхода.
		ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout)
			.OnCompleted(() => GetTree().Quit(exitCode));
	}
}
