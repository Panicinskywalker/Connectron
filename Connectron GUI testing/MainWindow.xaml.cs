using System.Drawing;
using System.Numerics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static Connectron_GUI_testing.MainWindow;
using ColorConverter = System.Windows.Media.ColorConverter;
using MediaColor = System.Windows.Media.Color;

namespace Connectron_GUI_testing
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // List of predefined colours that players will have, can be edited
        public readonly static List<MediaColor> colorList = new List<MediaColor>
        {
            Colors.Firebrick,
            Colors.Navy,
            Colors.DarkCyan,
            Colors.Chartreuse,
            Colors.DarkOrange,
            Colors.DeepPink,
            Colors.Aqua,
            Colors.BlueViolet,
            Colors.Gold,
            Colors.Salmon
        };

        public MainWindow()
        {
            InitializeComponent();
        }

        public async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (BestOfX.SelectedItem != null)
            {
                // Reset turn from the last game if not already done
                GameManager.ResetTurn();

                // Update GUI so that the player can see that it is the first players turn
                currentPlayerTextBlock.Text = $"Current player move: {GameManager.CurrentPlayerTurn} ";
                currentPlayerColourRectangle.Fill = new SolidColorBrush(colorList[0]);

                // Get inputs from the GUI on start and store this so that when the functions are called 
                // for each game of the best of it does not change even if the user does
                string bestOfString = BestOfX.SelectedItem.ToString();
                int bestOf = Convert.ToInt32(bestOfString?.Substring(bestOfString.Length - 1, 1));
                int numPlayers = Convert.ToInt32(PlayerNum.Text);
                int rows = Convert.ToInt32(RowNum.Text);
                int columns = Convert.ToInt32(ColumnNum.Text);
                GameManager.lineLengthToWin = Convert.ToInt32(LenToWin.Text);
                if ((bool)bombGameMode.IsChecked)
                {
                    GameManager.bombGameRule = true;
                    MessageBox.Show("Bomb is active, ctrl+click to use");
                }

                GameManager.Players = new List<Player>();

                // Initialise players for the game
                for (int i = 0; i < numPlayers; i++)
                {
                    Player player = new Player();
                    player.Score = 0;
                    player.RoundsWon = 0;
                    // If bomb is active then everyone gets their bomb set to true
                    player.HasBomb = GameManager.bombGameRule;
                    GameManager.Players.Add(player);
                }

                // For loop to carry out the best of x condition
                for (int round = 0; round < bestOf; round++)
                {
                    // Method to start the game and relevant other info
                    GameManager.InitialiseGame(numPlayers, myGrid);
                    GameManager.CreateClickableGrid(columns, rows, this);
                    GameManager.SetArea(columns, rows);
                    while (!GameManager.winner)
                    {
                        await Task.Delay(1000);
                    }
                    // Points won are based off num of people playing, round won +1
                    // If draw split then no points
                    if (GameManager.winner && !GameManager.draw)
                    {
                        GameManager.Players[GameManager.currentPlayerIndex].Score += numPlayers;
                        GameManager.Players[GameManager.currentPlayerIndex].RoundsWon += 1;
                    }

                    // Reset winner and draw
                    GameManager.winner = false;
                    GameManager.draw = false;
                }

                // Once the game is over and while has exited, display who won overall with x points
                int index = GameManager.Players
                            .Select((player, index) => new { Player = player, Index = index })
                            .Aggregate((a, b) => (a.Player.Score > b.Player.Score) ? a : b)
                            .Index;
                await Task.Delay(1000);
                MessageBox.Show($"Player {index + 1} has won with {GameManager.Players[index].Score} points");
                GameManager.ResetGrid();
                GameManager.ResetTurn();
                currentPlayerTextBlock.Text = $"Current player move: {GameManager.CurrentPlayerTurn} ";
                currentPlayerColourRectangle.Fill = new SolidColorBrush(colorList[0]);
            }
            else
            {
                // If the user does not enter a best of it will not start
                MessageBox.Show("Please Select a 'Best of' value.");
            }
        }

        // Method takes the click action and checks if there is a computer player playing or not
        // If there is it handles the player click then calls the computer to play
        // else if just does the player clicks as normal
        public void ClickableButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                // Retrieve the stored i and j values from the Tag property
                if (button.Tag is ButtonInfo buttonInfo)
                {
                    if (PlayerNum.Text == "1")
                    {
                        // Player turn
                        if (HandleButtonClick(button, buttonInfo, this).validMove == true)
                        {
                            // Computer turn
                            GameManager.SimulateComputerMove(this);
                        }
                        else
                        {
                            // If not valid move then exit the click
                            return;
                        }
                    }
                    else
                    {
                        // Continue as normal if there is > 1 players
                        HandleButtonClick(button, buttonInfo, this);
                    }
                }
            }
        }

        // Method to handle button click logic
        public (int squaresFilled, bool winner, bool validMove) HandleButtonClick(Button button, ButtonInfo buttonInfo, MainWindow mainWindow)
        {
            bool validMove = false;
            int i = buttonInfo.Row;
            int j = buttonInfo.Column;
            bool ctrlKeyPressed = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

            while (!validMove)
            {
                if (button.Background is SolidColorBrush brush && brush.Color == Brushes.LightGray.Color)
                {
                    // Choose colour for the click
                    MediaColor nextColor = colorList[GameManager.currentPlayerIndex];
                    buttonInfo.Colour = $"{nextColor}";

                    // Change the UI appearance to reflect the move
                    button.Background = new SolidColorBrush(nextColor);

                    GameManager.squaresFilled++;

                    // Method to move the square down if there is space below
                    // Then check to see if this makes a winning line
                    (i, j) = GameManager.Gravity(this, i, j);
                    GameManager.winner = GameManager.CheckWinLine(this, i, j);

                    // If it turns out someone has won display it and exit click
                    // main statement inside of startgame is now true
                    if (GameManager.winner == true)
                    {
                        MessageBox.Show($"Player {GameManager.CurrentPlayerTurn} has won the round!");
                        break;
                    }

                    // If draw is true then declare winner as true
                    // exit loop before as if win, in the point distribution check if draw before giving
                    if (GameManager.CheckDraw(GameManager.squaresFilled))
                    {
                        GameManager.draw = true;
                        GameManager.winner = true;
                        MessageBox.Show("Game Over: Draw!");
                        GameManager.squaresFilled = 0;
                        break;
                    }

                    // Check if the button was a ctrl+Click and if bombgamerule was active
                    if (ctrlKeyPressed && (GameManager.bombGameRule == true))
                    {
                        if (GameManager.Players[GameManager.currentPlayerIndex].HasBomb == true)
                        {
                            GameManager.BombCounter(this, i, j);
                        }
                        else
                        {
                            MessageBox.Show("You have already used your bomb counter!");
                        }
                      
                    }

                    // Go to next turn once all else is done
                    GameManager.NextTurn();

                    // Update GUI after turn moves so that it corretly displays who is next
                    currentPlayerTextBlock.Text = $"Current player move: {GameManager.CurrentPlayerTurn} ";
                    currentPlayerColourRectangle.Fill = new SolidColorBrush(colorList[GameManager.currentPlayerIndex]);

                    // Set valid move to true to exit the while and end the turn
                    validMove = true;
                }
                else
                {
                    // If the use clicks a square that is light gray (not player occupied)
                    // Then it exists the click and shows message
                    MessageBox.Show("This space is already occupied.");
                    break;
                }
            }

            return (GameManager.squaresFilled, GameManager.winner, validMove);
        }

        // Values for min max of inputs
        // Can be edited here and changed throughout
        private readonly int minColumnValue = 5;
        private readonly int maxColumnValue = 100;
        private readonly int minRowValue = 5;
        private readonly int maxRowValue = 100;
        private readonly int minPlayerValue = 1;
        private readonly int maxPlayerValue = 10;
        private readonly int minWinValue = 4;
        private readonly int maxWinValue = 10;

        // Inputs from user
        // WPF forms doesn't have IntegerUpDown so made my own
        // Allow user to increase/decrease values, if they choose to directly edit
        // Textbox to something out of range it will default back to min on lostfocus
        private void IncreaseButton_Click_Column(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(ColumnNum.Text, out int value))
            {
                value++;

                if (value <= maxColumnValue)
                {
                    ColumnNum.Text = value.ToString();
                }
                else
                {
                    ColumnNum.Text = maxColumnValue.ToString();
                }
            }
        }
        private void DecreaseButton_Click_Column(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(ColumnNum.Text, out int value))
            {
                value--;

                if (value >= minColumnValue)
                {
                    ColumnNum.Text = value.ToString();
                }
                else
                {
                    ColumnNum.Text = minColumnValue.ToString();
                }
            }
        }
        private void ColumnNum_LostFocus(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(ColumnNum.Text, out int value) && (value < minColumnValue || value > maxColumnValue))
            {
                ColumnNum.Text = minColumnValue.ToString();
            }
        }
        private void IncreaseButton_Click_Rows(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(RowNum.Text, out int value))
            {
                value++;

                if (value <= maxRowValue)
                {
                    RowNum.Text = value.ToString();
                }
                else
                {
                    RowNum.Text = maxRowValue.ToString();
                }
            }
        }
        private void DecreaseButton_Click_Rows(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(RowNum.Text, out int value))
            {
                value--;

                if (value >= minRowValue)
                {
                    RowNum.Text = value.ToString();
                }
                else
                {
                    RowNum.Text = minRowValue.ToString();
                }
            }
        }
        private void RowNum_LostFocus(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(RowNum.Text, out int value) && (value < minRowValue || value > maxRowValue))
            {
                RowNum.Text = minRowValue.ToString();
            }
        }
        private void IncreaseButton_Click_Players(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(PlayerNum.Text, out int value))
            {
                value++;

                if (value <= maxPlayerValue)
                {
                    PlayerNum.Text = value.ToString();
                }
                else
                {
                    PlayerNum.Text = maxPlayerValue.ToString();
                }
            }
        }
        private void DecreaseButton_Click_Players(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(PlayerNum.Text, out int value))
            {
                value--;

                if (value >= minPlayerValue)
                {
                    PlayerNum.Text = value.ToString();
                }
                else
                {
                    PlayerNum.Text = maxPlayerValue.ToString();
                }
            }
        }
        private void PlayerNum_LostFocus(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(PlayerNum.Text, out int value) && (value < minPlayerValue || value > maxPlayerValue))
            {
                PlayerNum.Text = minPlayerValue.ToString();
            }
        }
        private void IncreaseButton_Click_Len(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(LenToWin.Text, out int value))
            {
                value++;

                if (value <= maxWinValue)
                {
                    LenToWin.Text = value.ToString();
                }
                else
                {
                    LenToWin.Text = maxWinValue.ToString();
                }
            }
        }
        private void DecreaseButton_Click_Len(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(LenToWin.Text, out int value))
            {
                value--;

                if (value >= minWinValue)
                {
                    LenToWin.Text = value.ToString();
                }
                else
                {
                    LenToWin.Text = maxWinValue.ToString();
                }
            }
        }
        private void LenToWin_LostFocus(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(LenToWin.Text, out int value) && (value < minWinValue|| value > maxWinValue))
            {
                LenToWin.Text = minWinValue.ToString();
            }
        }
        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string BestOf = BestOfX.SelectedItem.ToString();
        }
    }

    // Simple class to hold i and j values and colour of buttons
    public class ButtonInfo
    {
        public int Row { get; set; }
        public int Column { get; set; }
        public string? Colour { get; set; }
    }

    // Class to store current scores/round wins
    public class Player
    {
        public int Score { get; set; }
        public int RoundsWon { get; set; }
        public bool HasBomb { get; set; }
    }

    // Seperate GameManager class so that I can globally access all game related methods
    public class GameManager
    {
        public static int currentPlayerIndex = 0;
        public static int lineLengthToWin = 4;
        private static int numberOfPlayers;
        private static int gridArea;
        private static Grid? myGrid;
        public static bool draw = false;
        private static bool debug = false; // For the win line check
        public static bool winner = false;
        public static int squaresFilled = 0;
        public static int numRoundsPlayed = 0;
        public static bool bombGameRule = false;
        public static List<Player>? Players { get; set; }

        // Static property to access the current player turn
        // Add 1 to convert to 1-based index for user interface
        public static int CurrentPlayerTurn
        {
            get { return currentPlayerIndex + 1; }
        }

        // Method to switch to the next player turn
        public static void NextTurn()
        {
            currentPlayerIndex = (currentPlayerIndex + 1) % numberOfPlayers;

            // If currentPlayerIndex becomes 0 after incrementing the turns get reset.
            if (currentPlayerIndex == 0)
            {
                ResetTurn();
            }
        }

        // Method to reset the turn back to the first player
        public static void ResetTurn()
        {
            currentPlayerIndex = 0;
        }

        // Method to initialize the number of players
        public static void InitialiseGame(int num, Grid grid)
        {
            numberOfPlayers = num;
            myGrid = grid;

            currentPlayerIndex = 0;

            // Automatically introduce a second computer player if there's only one player
            if (numberOfPlayers == 1)
            {
                numberOfPlayers++;
            }
        }

        // Method to initialise the Grid based on user parameters
        public static void CreateClickableGrid(int columns, int rows, MainWindow mainWindow)
        {
            // Clear existing content from the grid
            GameManager.ResetGrid();

            // Create rows and columns based on user input
            for (int i = 0; i < rows; i++)
            {
                myGrid.RowDefinitions.Add(new RowDefinition());
            }

            for (int j = 0; j < columns; j++)
            {
                myGrid.ColumnDefinitions.Add(new ColumnDefinition());
            }

            myGrid.Background = Brushes.Black;

            // For each button i the grid add the relevant info
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < columns; j++)
                {
                    var button = new Button
                    {
                        // Debug statement to show coords of each button
                        //Content = $"{i + 1}, {j + 1}",
                        Background = Brushes.LightGray,
                        BorderBrush = Brushes.Black,
                        BorderThickness = new Thickness(1),
                        Focusable = false,
                        HorizontalContentAlignment = HorizontalAlignment.Stretch,
                        VerticalContentAlignment = VerticalAlignment.Stretch,
                        Width = double.NaN,
                        Height = double.NaN
                    };

                    button.Loaded += (sender, e) =>
                    {
                        ApplyCircularBorder(button);
                    };

                    // Add the button to the ButtonInfo class
                    var buttonInfo = new ButtonInfo { Row = i, Column = j, Colour = $"{Colors.LightGray}" };


                    // Set the Tag property of the button to store the i and j values
                    button.Tag = buttonInfo;

                    // Attach a click event handler to each button
                    button.Click += mainWindow.ClickableButton_Click;

                    // Set row and column for the button
                    Grid.SetRow(button, i);
                    Grid.SetColumn(button, j);

                    // Add the button to the grid
                    myGrid.Children.Add(button);
                }
            }
        }

        // Method to make buttons round when intialised
        private static void ApplyCircularBorder(Button button)
        {
            var border = VisualTreeHelper.GetChild(button, 0) as Border;

            if (border != null)
            {
                double radius = Math.Min(button.ActualWidth, button.ActualHeight) / 2;
                border.CornerRadius = new CornerRadius(radius);
            }
        }

        // Method to reset the grid
        public static void ResetGrid()
        {
            myGrid.Children.Clear();
            myGrid.RowDefinitions.Clear();
            myGrid.ColumnDefinitions.Clear();
            myGrid.Background = Brushes.White;
        }

        // Method to set the area so I can check for draws
        public static void SetArea(int column, int row) 
        {
            gridArea = column * row;
        }

        // Method to check if there is a draw
        public static bool CheckDraw(int squaresFilled) 
        {
            bool isDraw;
            if (squaresFilled < gridArea)
            {
                isDraw = false;
            }
            else
            {
                isDraw = true;
            }
            return isDraw;
        }

        // Method to introduce a computer player (if playing with 1 player)
        public static void SimulateComputerMove(MainWindow mainWindow)
        {
            ButtonInfo randomButton = GetRandomAvailableButton(mainWindow.myGrid);
            if (randomButton != null)
            {
                Button button = GetButtonFromInfo(randomButton, mainWindow.myGrid);
                if (button != null)
                {
                    randomButton.Colour = $"{Colors.DarkCyan}";
                    mainWindow.HandleButtonClick(button, randomButton, mainWindow);
                }
            }
        }

        // Method to find all available buttons
        public static List<ButtonInfo> GetAvailableButtons(Grid myGrid)
        {
            List<ButtonInfo> availableButtons = new List<ButtonInfo>();

            foreach (var button in myGrid.Children.OfType<Button>())
            {
                if (button.Tag is ButtonInfo buttonInfo && (button.Background is SolidColorBrush brush && brush.Color == Brushes.LightGray.Color))
                {
                    availableButtons.Add(buttonInfo);
                }
            }

            return availableButtons;
        }

        // Method to get a random available buttons so the computer knows where to move
        public static ButtonInfo? GetRandomAvailableButton(Grid myGrid)
        {
            List<ButtonInfo> availableButtons = GetAvailableButtons(myGrid);
            return availableButtons.Count > 0 ? availableButtons[new Random().Next(availableButtons.Count)] : null;
        }

        // Method to find the ButtonInfo of a button in myGrid
        public static Button? GetButtonFromInfo(ButtonInfo buttonInfo, Grid myGrid)
        {
            foreach (var button in myGrid.Children.OfType<Button>())
            {
                if (button.Tag is ButtonInfo info && info.Equals(buttonInfo))
                {
                    return button;
                }
            }

            return null;
        }

        // Method to find the position in myGrid that matches a ButtonInfo
        private static Button FindButtonInGrid(Grid grid, ButtonInfo buttonInfo)
        {
            if (buttonInfo == null)
            {
                return null;
            }

            foreach (UIElement element in grid.Children)
            {
                if (element is Button button && Grid.GetRow(button) == buttonInfo.Row && Grid.GetColumn(button) == buttonInfo.Column)
                {
                    return button;
                }
            }

            return null;
        }

        // Method to get all buttons in a grid into a list
        // This is for gravity and also for checking win lines
        public static List<ButtonInfo> GetAllButtons(Grid myGrid)
        {
            List<ButtonInfo> allButtons = new List<ButtonInfo>();

            foreach (var button in myGrid.Children.OfType<Button>())
            {
                if (button.Tag is ButtonInfo buttonInfo)
                {
                    allButtons.Add(buttonInfo);
                }
            }

            return allButtons;
        }

        // Gravity Method so that clicked squares fall down the grid if square below != occupied
        // Returns the i and j values of where it lands
        public static (int FinalRow, int FinalColumn) Gravity(MainWindow mainWindow, int i, int j)
        {
            bool doneFalling = false;

            do
            {
                List<ButtonInfo> availableButtons = GetAvailableButtons(myGrid);
                ButtonInfo buttonBelow = availableButtons.FirstOrDefault(button => button.Row == i + 1 && button.Column == j);

                List<ButtonInfo> allButtons = GetAllButtons(myGrid);
                ButtonInfo currentButton = allButtons.FirstOrDefault(button => button.Row == i && button.Column == j);

                // Exit the function if there is no button below or the button below has a different color 
                // Additional if to check if also LightGray for bomb function as need to make buttons fall but only coloured ones
                if (buttonBelow == null || buttonBelow.Colour != $"{Colors.LightGray}" || currentButton.Colour == $"{Colors.LightGray}")
                {
                    return (FinalRow: i, FinalColumn: j);
                }

                Button currButton = FindButtonInGrid(myGrid, currentButton);
                Button beloButton = FindButtonInGrid(myGrid, buttonBelow);
                beloButton.Background = currButton.Background;
                buttonBelow.Colour = currentButton.Colour;

                currButton.Background = Brushes.LightGray;
                currentButton.Colour = $"{Colors.LightGray}";

                i++;

                if (buttonBelow == null || buttonBelow.Colour == $"{Colors.LightGray}")
                {
                    doneFalling = true;
                }

            } while (!doneFalling);

            return (FinalRow: i, FinalColumn: j);
        }

        // Method to determine if someone has a line of the correct length to win the game
        // NORTH: i--; EAST: j++; SOUTH: i++; WEST: j--;
        public static bool CheckWinLine(MainWindow mainWindow, int i, int j) 
        {
            if (CheckHorizontal(mainWindow, i, j) == true || CheckVertical(mainWindow, i, j) == true || CheckDiagonal(mainWindow, i, j) == true)
            {
                return true;
            }
            return false;
        }

        // Basic principle for all 3 methods is to checked 'lineLengthToWin' squares in each direction
        // it goes outwards from the clicked button and if a square != the colour of the current player it stops checking that direction
        private static bool CheckHorizontal(MainWindow mainWindow, int i, int j)
        {
            int count = 0;
            List<ButtonInfo> allButtons = GetAllButtons(myGrid);
            
            // Checking East
            for (int x = 0; x < lineLengthToWin; x++)
            {
                ButtonInfo currentButton = allButtons.FirstOrDefault(button => button.Row == i && button.Column == j + x);
                if (debug == true)
                {
                    if (currentButton != null)
                    {
                        Button button = FindButtonInGrid(myGrid, currentButton);
                        button.Background = Brushes.Red;
                    }
                }
                else
                {
                    if (currentButton == null || currentButton.Colour != $"{colorList[currentPlayerIndex]}")
                    {
                        break;
                    }
                    count++;
                }
            }
            
            if (count == lineLengthToWin)
            {
                return true;
            }

            count = 0;

            // Checking West
            for (int x = 0; x < lineLengthToWin; x++)
            {
                ButtonInfo currentButton = allButtons.FirstOrDefault(button => button.Row == i && button.Column == j - x);
                if (debug == true)
                {
                    if (currentButton != null)
                    {
                        Button button = FindButtonInGrid(myGrid, currentButton);
                        button.Background = Brushes.Red;
                    }
                }
                else
                {
                    if (currentButton == null || currentButton.Colour != $"{colorList[currentPlayerIndex]}")
                    {
                        break;
                    }
                    count++;
                }
            }

            if (count == lineLengthToWin)
            {
                return true;
            }

            return false;
        }
        private static bool CheckVertical(MainWindow mainWindow, int i, int j) 
        {
            int count = 0;
            List<ButtonInfo> allButtons = GetAllButtons(myGrid);

            // Checking North
            for (int x = 0; x < lineLengthToWin; x++)
            {
                ButtonInfo currentButton = allButtons.FirstOrDefault(button => button.Row == i - x && button.Column == j);
                if (debug == true)
                {
                    if (currentButton != null)
                    {
                        Button button = FindButtonInGrid(myGrid, currentButton);
                        button.Background = Brushes.Red;
                    }
                }
                else
                {
                    if (currentButton == null || currentButton.Colour != $"{colorList[currentPlayerIndex]}")
                    {
                        break;
                    }
                    count++;
                }
            }

            if (count == lineLengthToWin)
            {
                return true;
            }

            count = 0;

            // Checking South
            for (int x = 0; x < lineLengthToWin; x++)
            {
                ButtonInfo currentButton = allButtons.FirstOrDefault(button => button.Row == i + x && button.Column == j);
                if (debug == true)
                {
                    if (currentButton != null)
                    {
                        Button button = FindButtonInGrid(myGrid, currentButton);
                        button.Background = Brushes.Red;
                    }
                }
                else
                {
                    if (currentButton == null || currentButton.Colour != $"{colorList[currentPlayerIndex]}")
                    {
                        break;
                    }
                    count++;
                }
            }

            if (count == lineLengthToWin)
            {
                return true;
            }

            return false;
        }
        private static bool CheckDiagonal(MainWindow mainWindow, int i, int j)
        {
            int count = 0;
            List<ButtonInfo> allButtons = GetAllButtons(myGrid);

            // Checking North-East
            for (int x = 0; x < lineLengthToWin; x++)
            {
                ButtonInfo currentButton = allButtons.FirstOrDefault(button => button.Row == i - x && button.Column == j + x);
                if (debug == true)
                {
                    if (currentButton != null)
                    {
                        Button button = FindButtonInGrid(myGrid, currentButton);
                        button.Background = Brushes.Red;
                    }
                }
                else
                {
                    if (currentButton == null || currentButton.Colour != $"{colorList[currentPlayerIndex]}")
                    {
                        break;
                    }
                    count++;
                }
            }

            if (count == lineLengthToWin)
            {
                return true;
            }

            count = 0;

            // Checking South-East
            for (int x = 0; x < lineLengthToWin; x++)
            {
                ButtonInfo currentButton = allButtons.FirstOrDefault(button => button.Row == i + x && button.Column == j + x);
                if (debug == true)
                {
                    if (currentButton != null)
                    {
                        Button button = FindButtonInGrid(myGrid, currentButton);
                        button.Background = Brushes.Red;
                    }
                }
                else
                {
                    if (currentButton == null || currentButton.Colour != $"{colorList[currentPlayerIndex]}")
                    {
                        break;
                    }
                    count++;
                }
            }

            if (count == lineLengthToWin)
            {
                return true;
            }

            count = 0;

            // Checking North-West
            for (int x = 0; x < lineLengthToWin; x++)
            {
                ButtonInfo currentButton = allButtons.FirstOrDefault(button => button.Row == i - x && button.Column == j - x);
                if (debug == true)
                {
                    if (currentButton != null)
                    {
                        Button button = FindButtonInGrid(myGrid, currentButton);
                        button.Background = Brushes.Red;
                    }
                }
                else
                {
                    if (currentButton == null || currentButton.Colour != $"{colorList[currentPlayerIndex]}")
                    {
                        break;
                    }
                    count++;
                }
            }

            if (count == lineLengthToWin)
            {
                return true;
            }

            count = 0;

            // Checking South-West
            for (int x = 0; x < lineLengthToWin; x++)
            {
                ButtonInfo currentButton = allButtons.FirstOrDefault(button => button.Row == i + x && button.Column == j - x);
                if (debug == true)
                {
                    if (currentButton != null)
                    {
                        Button button = FindButtonInGrid(myGrid, currentButton);
                        button.Background = Brushes.Red;
                    }
                }
                else
                {
                    if (currentButton == null || currentButton.Colour != $"{colorList[currentPlayerIndex]}")
                    {
                        break;
                    }
                    count++;
                }
            }

            if (count == lineLengthToWin)
            {
                return true;
            }

            return false;
        }

        // Method to carry out the bomb function
        public static void BombCounter(MainWindow mainWindow, int i, int j)
        {
            List<ButtonInfo> allButtons = GetAllButtons(myGrid);

            // Use 2 for loops to get the 9 buttons that will be cleared
            for (int x = -1; x < 2; x++)
            {
                for (int y = -1; y < 2; y++)
                {
                    ButtonInfo currentButton = allButtons.FirstOrDefault(button => button.Row == i + x && button.Column == j + y);

                    if (currentButton == null)
                    {
                        break;
                    }

                    // Set the affected buttons back to default - "bombed"
                    currentButton.Colour = $"{Colors.LightGray}";
                    Button button = FindButtonInGrid(myGrid, currentButton);
                    button.Background = Brushes.LightGray;
                }
            }

            // Repeat the updated falling as there may be multiple counters waiting to fall
            for (int k = 0; k < gridArea; k++)
            {
                foreach (ButtonInfo button in allButtons)
                {
                    i = button.Row;
                    j = button.Column;
                    (i, j) = Gravity(mainWindow, i, j);
                }
            }

            // Set the players bomb to false as it has been used
            Players[GameManager.currentPlayerIndex].HasBomb = false;
        }
    }
}