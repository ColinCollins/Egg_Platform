using UnityEngine;

public enum Player
{
    None = 0,
    X = 1,
    O = 2
}

public enum GameState
{
    Playing,
    XWin,
    OWin,
    Draw
}

public class TicTacToeGame : MonoBehaviour
{
    [Header("Game Settings")]
    public int boardSize = 3;
    
    private Player[,] board;
    private Player currentPlayer = Player.X;
    private GameState gameState = GameState.Playing;
    
    public System.Action<Player, int, int> OnCellChanged;
    public System.Action<Player> OnPlayerChanged;
    public System.Action<GameState> OnGameStateChanged;
    
    void Start()
    {
        InitializeBoard();
    }
    
    public void InitializeBoard()
    {
        board = new Player[boardSize, boardSize];
        currentPlayer = Player.X;
        gameState = GameState.Playing;
        
        for (int i = 0; i < boardSize; i++)
        {
            for (int j = 0; j < boardSize; j++)
            {
                board[i, j] = Player.None;
            }
        }
        
        OnPlayerChanged?.Invoke(currentPlayer);
        OnGameStateChanged?.Invoke(gameState);
    }
    
    public bool MakeMove(int row, int col)
    {
        if (gameState != GameState.Playing)
        {
            return false;
        }
        
        if (row < 0 || row >= boardSize || col < 0 || col >= boardSize)
        {
            return false;
        }
        
        if (board[row, col] != Player.None)
        {
            return false;
        }
        
        board[row, col] = currentPlayer;
        OnCellChanged?.Invoke(currentPlayer, row, col);
        
        CheckGameState();
        
        if (gameState == GameState.Playing)
        {
            currentPlayer = (currentPlayer == Player.X) ? Player.O : Player.X;
            OnPlayerChanged?.Invoke(currentPlayer);
        }
        
        return true;
    }
    
    private void CheckGameState()
    {
        Player winner = CheckWinner();
        
        if (winner != Player.None)
        {
            gameState = (winner == Player.X) ? GameState.XWin : GameState.OWin;
            OnGameStateChanged?.Invoke(gameState);
            return;
        }
        
        if (IsBoardFull())
        {
            gameState = GameState.Draw;
            OnGameStateChanged?.Invoke(gameState);
            return;
        }
    }
    
    private Player CheckWinner()
    {
        for (int i = 0; i < boardSize; i++)
        {
            if (board[i, 0] != Player.None && 
                board[i, 0] == board[i, 1] && 
                board[i, 1] == board[i, 2])
            {
                return board[i, 0];
            }
            
            if (board[0, i] != Player.None && 
                board[0, i] == board[1, i] && 
                board[1, i] == board[2, i])
            {
                return board[0, i];
            }
        }
        
        if (board[0, 0] != Player.None && 
            board[0, 0] == board[1, 1] && 
            board[1, 1] == board[2, 2])
        {
            return board[0, 0];
        }
        
        if (board[0, 2] != Player.None && 
            board[0, 2] == board[1, 1] && 
            board[1, 1] == board[2, 0])
        {
            return board[0, 2];
        }
        
        return Player.None;
    }
    
    private bool IsBoardFull()
    {
        for (int i = 0; i < boardSize; i++)
        {
            for (int j = 0; j < boardSize; j++)
            {
                if (board[i, j] == Player.None)
                {
                    return false;
                }
            }
        }
        return true;
    }
    
    public Player GetCurrentPlayer()
    {
        return currentPlayer;
    }
    
    public GameState GetGameState()
    {
        return gameState;
    }
    
    public Player GetCell(int row, int col)
    {
        if (row < 0 || row >= boardSize || col < 0 || col >= boardSize)
        {
            return Player.None;
        }
        return board[row, col];
    }
}

