using UnityEngine;
using UnityEngine.UI;

public class TicTacToeCell : MonoBehaviour
{
    [Header("References")]
    public Button button;
    public Text text;
    
    private int row;
    private int col;
    private TicTacToeUI ui;
    private Player currentPlayer = Player.None;
    
    void Start()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }
        
        if (button != null)
        {
            button.onClick.AddListener(OnButtonClicked);
        }
        
        if (text == null)
        {
            text = GetComponentInChildren<Text>();
        }
        
        UpdateDisplay();
    }
    
    public void Initialize(int row, int col, TicTacToeUI ui)
    {
        this.row = row;
        this.col = col;
        this.ui = ui;
    }
    
    public void SetPlayer(Player player)
    {
        currentPlayer = player;
        UpdateDisplay();
    }
    
    private void UpdateDisplay()
    {
        if (text != null)
        {
            switch (currentPlayer)
            {
                case Player.X:
                    text.text = "X";
                    text.color = Color.red;
                    break;
                case Player.O:
                    text.text = "O";
                    text.color = Color.blue;
                    break;
                default:
                    text.text = "";
                    break;
            }
        }
        
        if (button != null)
        {
            button.interactable = (currentPlayer == Player.None);
        }
    }
    
    private void OnButtonClicked()
    {
        if (currentPlayer == Player.None && ui != null)
        {
            ui.OnCellClicked(row, col);
        }
    }
}

