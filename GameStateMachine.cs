using System;
using UnityEngine;
using UnityHFSM;

public class GameStateMachine : MonoBehaviour
{
    StateMachine sm;

    private void Awake()
    {
        CreateGameStateMachine();
    }

    private void CreateGameStateMachine()
    {
        var data = new SharedData();
        
        // 1 initial ui state, we waiting for player to press button
        // 2 trigger button enter game state, launch ecs core
        // 3 in the middle of the game press ecs, enter pause state
        sm = new StateMachine();
        sm.AddState(nameof(InitialMenuState), new InitialMenuState());
        sm.AddState(nameof(LaunchGameplayState), new LaunchGameplayState(data));
        sm.AddState(nameof(GameplayState), new GameplayState());
        sm.AddState(nameof(PauseState), new PauseState());
        sm.AddState(nameof(WinState), new WinState());
        sm.AddState(nameof(DisposeGameplayState), new DisposeGameplayState(data));

        sm.AddTransition(nameof(InitialMenuState), nameof(LaunchGameplayState), _ => Input.GetKeyDown(KeyCode.Space));
        sm.AddTransition(nameof(GameplayState), nameof(PauseState), _ => Input.GetKeyDown(KeyCode.Escape));
        sm.AddTransition(nameof(PauseState), nameof(GameplayState), _ => Input.GetKeyDown(KeyCode.Escape));
        sm.AddTransition(nameof(GameplayState), nameof(WinState), _ => data.isWon);
        sm.AddTransition(nameof(WinState), nameof(DisposeGameplayState), _ => Input.GetKeyDown(KeyCode.Return));
        
        sm.AddTransition(nameof(DisposeGameplayState), nameof(LaunchGameplayState));
        sm.AddTransition(nameof(LaunchGameplayState), nameof(GameplayState));

        sm.Init();
    }

    private void Update()
    {
        sm.OnLogic();
    }
}

public class InitialMenuState : State
{
    public InitialMenuState()
    {
    }

    public override void OnEnter()
    {
        StartGameText.text = "Press Space to Start Game";
    }
    
}

public class LaunchGameplayState : State
{
    private SharedData data;

    public LaunchGameplayState(SharedData data)
    {
        this.data = data;
    }

    public override void OnEnter()
    {
        //start game
        GameManager manager = new GameObject("Game manager").AddComponent<GameManager>();
        data.target = 10;
        manager.StartGame(data);
    }
}

public class GameplayState : State
{
    public override void OnEnter()
    {
        StartGameText.text = "";
        GameObject.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include).gameObject.SetActive(true);
        Time.timeScale = 1;
    }

    public override void OnExit()
    {
        GameObject.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include).gameObject.SetActive(false);
        Time.timeScale = 0;
    }
}

public class PauseState : State
{
    public override void OnEnter()
    {
        StartGameText.text = "Pause";
    }
}

public class WinState : State
{
    public override void OnEnter()
    {
        StartGameText.text = "WIN!\nPress Enter to continue";
    }
}

public class DisposeGameplayState : State
{
    private SharedData data;

    public DisposeGameplayState(SharedData data)
    {
        this.data = data;
    }

    public override void OnEnter()
    {
        GameManager gm = GameObject.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        GameObject.Destroy(gm.gameObject);
        
        data.Reset();
    }
}

