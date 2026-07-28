using UnityEngine;
[DefaultExecutionOrder(-100)]
public class GameManager : MonoBehaviour
{
    
    public static GameManager Instance { get; private set; }
    
    public GameState State { get; private set; } = GameState.Boot;
    
    


    void Start()
    {
        
    }

    void Update()
    {
        
    }
}
