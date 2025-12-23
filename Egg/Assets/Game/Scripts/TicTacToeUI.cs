using UnityEngine;
using UnityEngine.UI;

public class TicTacToeUI : MonoBehaviour
{
    [Header("References")]
    public TicTacToeGame game;
    public Transform boardParent;
    public Text statusText;
    public Button restartButton;
    public GameObject cellPrefab;
    
    private TicTacToeCell[,] cells;
    private int boardSize = 3;
    
    void Start()
    {
        if (game == null)
        {
            game = FindObjectOfType<TicTacToeGame>();
        }
        
        if (game == null)
        {
            GameObject gameObj = new GameObject("TicTacToeGame");
            game = gameObj.AddComponent<TicTacToeGame>();
        }
        
        if (game != null)
        {
            boardSize = game.boardSize;
            game.OnCellChanged += OnCellChanged;
            game.OnPlayerChanged += OnPlayerChanged;
            game.OnGameStateChanged += OnGameStateChanged;
        }
        
        if (restartButton != null)
        {
            restartButton.onClick.AddListener(RestartGame);
        }
        
        CreateBoard();
        UpdateStatus();
    }
    
    void CreateBoard()
    {
        if (boardParent == null || cellPrefab == null)
        {
            return;
        }
        
        cells = new TicTacToeCell[boardSize, boardSize];
        
        for (int i = 0; i < boardSize; i++)
        {
            for (int j = 0; j < boardSize; j++)
            {
                GameObject cellObj = Instantiate(cellPrefab, boardParent);
                TicTacToeCell cell = cellObj.GetComponent<TicTacToeCell>();
                
                if (cell != null)
                {
                    cell.Initialize(i, j, this);
                    cells[i, j] = cell;
                }
            }
        }
    }
    
    public void OnCellClicked(int row, int col)
    {
        if (game != null)
        {
            game.MakeMove(row, col);
        }
    }
    
    private void OnCellChanged(Player player, int row, int col)
    {
        if (cells != null && row >= 0 && row < boardSize && col >= 0 && col < boardSize)
        {
            if (cells[row, col] != null)
            {
                cells[row, col].SetPlayer(player);
            }
        }
    }
    
    private void OnPlayerChanged(Player player)
    {
        UpdateStatus();
    }
    
    private void OnGameStateChanged(GameState state)
    {
        UpdateStatus();
    }
    
    private void UpdateStatus()
    {
        if (statusText == null || game == null)
        {
            return;
        }
        
        GameState state = game.GetGameState();
        
        switch (state)
        {
            case GameState.Playing:
                Player current = game.GetCurrentPlayer();
                statusText.text = $"当前玩家: {(current == Player.X ? "X" : "O")}";
                break;
            case GameState.XWin:
                statusText.text = "X 获胜！";
                break;
            case GameState.OWin:
                statusText.text = "O 获胜！";
                break;
            case GameState.Draw:
                statusText.text = "平局！";
                break;
        }
    }
    
    public void RestartGame()
    {
        if (game != null)
        {
            game.InitializeBoard();
        }
        
        if (cells != null)
        {
            for (int i = 0; i < boardSize; i++)
            {
                for (int j = 0; j < boardSize; j++)
                {
                    if (cells[i, j] != null)
                    {
                        cells[i, j].SetPlayer(Player.None);
                    }
                }
            }
        }
        
        UpdateStatus();
    }
}

