using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the game's UI canvas screens and grid cell visuals.
/// Provides methods to switch between screens and to update
/// individual grid cells based on shot results.
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Canvas Screens")]
    [SerializeField] private GameObject mainMenuScreen;
    [SerializeField] private GameObject waitingRoomScreen;
    [SerializeField] private GameObject placingScreen;
    [SerializeField] private GameObject battleScreen;
    [SerializeField] private GameObject gameOverScreen;

    [Header("Battle Grids")]
    [Tooltip("10×10 grid of Buttons representing the opponent's board (where the player shoots).")]
    [SerializeField] private Button[] attackGrid = new Button[Tablero.BoardSize * Tablero.BoardSize];

    [Tooltip("10×10 grid of Images representing the player's own board (incoming shots).")]
    [SerializeField] private Image[] defenseGrid = new Image[Tablero.BoardSize * Tablero.BoardSize];

    [Header("HUD")]
    [SerializeField] private TextMeshProUGUI turnLabel;
    [SerializeField] private TextMeshProUGUI timerLabel;
    [SerializeField] private TextMeshProUGUI gameOverLabel;

    [Header("Shot Result Colors")]
    [SerializeField] private Color hitColor = Color.red;
    [SerializeField] private Color missColor = Color.gray;
    [SerializeField] private Color sunkColor = new Color(0.5f, 0f, 0f, 1f); // dark red

    /// <summary>Shows the Main Menu screen and hides every other screen.</summary>
    public void ShowMainMenu()
    {
        SetActiveScreen(mainMenuScreen);
    }

    /// <summary>Shows the Waiting Room screen.</summary>
    public void ShowWaitingRoom()
    {
        SetActiveScreen(waitingRoomScreen);
    }

    /// <summary>Shows the Placing screen.</summary>
    public void ShowPlacing()
    {
        SetActiveScreen(placingScreen);
    }

    /// <summary>Shows the Battle screen.</summary>
    public void ShowBattle()
    {
        SetActiveScreen(battleScreen);
    }

    /// <summary>
    /// Shows the Game Over screen and displays the result message.
    /// </summary>
    /// <param name="message">Text shown to the player (e.g. "You Win!" or "You Lose!").</param>
    public void ShowGameOver(string message)
    {
        SetActiveScreen(gameOverScreen);
        if (gameOverLabel != null)
            gameOverLabel.text = message;
    }

    /// <summary>
    /// Updates the placement timer display.
    /// </summary>
    /// <param name="secondsRemaining">Seconds left in the placement phase.</param>
    public void UpdateTimerDisplay(int secondsRemaining)
    {
        if (timerLabel != null)
            timerLabel.text = $"Time: {secondsRemaining}s";
    }

    /// <summary>
    /// Updates the turn indicator label.
    /// </summary>
    /// <param name="text">The text to display (e.g. "Your Turn" or "Opponent's Turn").</param>
    public void UpdateTurnLabel(string text)
    {
        if (turnLabel != null)
            turnLabel.text = text;
    }

    /// <summary>
    /// Enables or disables all buttons in the attack grid.
    /// Buttons should only be interactable during the player's turn.
    /// </summary>
    /// <param name="interactable">True to enable, false to disable.</param>
    public void SetAttackGridInteractable(bool interactable)
    {
        if (attackGrid == null) return;
        for (int i = 0; i < attackGrid.Length; i++)
        {
            if (attackGrid[i] != null)
                attackGrid[i].interactable = interactable;
        }
    }

    /// <summary>
    /// Updates a cell on the attack grid (opponent's board) to reflect a shot result.
    /// </summary>
    /// <param name="x">Column (0-based).</param>
    /// <param name="y">Row (0-based).</param>
    /// <param name="result">The result of the shot.</param>
    public void UpdateAttackCell(int x, int y, ShotResult result)
    {
        int index = y * Tablero.BoardSize + x;
        if (attackGrid == null || index < 0 || index >= attackGrid.Length || attackGrid[index] == null)
            return;

        Image image = attackGrid[index].GetComponent<Image>();
        if (image != null)
            image.color = GetColorForResult(result);

        // Disable the button so the player cannot shoot the same cell twice.
        attackGrid[index].interactable = false;
    }

    /// <summary>
    /// Updates a cell on the defense grid (player's own board) to reflect an incoming shot.
    /// </summary>
    /// <param name="x">Column (0-based).</param>
    /// <param name="y">Row (0-based).</param>
    /// <param name="result">The result of the shot.</param>
    public void UpdateDefenseCell(int x, int y, ShotResult result)
    {
        int index = y * Tablero.BoardSize + x;
        if (defenseGrid == null || index < 0 || index >= defenseGrid.Length || defenseGrid[index] == null)
            return;

        defenseGrid[index].color = GetColorForResult(result);
    }

    /// <summary>
    /// Returns the color associated with a given shot result.
    /// </summary>
    /// <param name="result">The shot result.</param>
    /// <returns>A Color corresponding to HIT, MISS, or SUNK.</returns>
    private Color GetColorForResult(ShotResult result)
    {
        switch (result)
        {
            case ShotResult.HIT:  return hitColor;
            case ShotResult.MISS: return missColor;
            case ShotResult.SUNK: return sunkColor;
            default:              return Color.white;
        }
    }

    /// <summary>
    /// Activates the specified screen and deactivates all others.
    /// </summary>
    /// <param name="activeScreen">The screen GameObject to show.</param>
    private void SetActiveScreen(GameObject activeScreen)
    {
        if (mainMenuScreen != null)    mainMenuScreen.SetActive(activeScreen == mainMenuScreen);
        if (waitingRoomScreen != null)  waitingRoomScreen.SetActive(activeScreen == waitingRoomScreen);
        if (placingScreen != null)      placingScreen.SetActive(activeScreen == placingScreen);
        if (battleScreen != null)       battleScreen.SetActive(activeScreen == battleScreen);
        if (gameOverScreen != null)     gameOverScreen.SetActive(activeScreen == gameOverScreen);
    }
}
