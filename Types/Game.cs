using System.Text;
using Discord;
using Discord.WebSocket;

namespace UNO.Types
{
    public class Game
    {
        /// <summary>
        /// The Host of the game
        /// </summary>
        public Player Host { get; set; }

        /// <summary>
        /// The list of players (INCLUDING the host)
        /// </summary>
        public List<Player> Players { get; set; }

        /// <summary>
        /// The random object used to generate cards
        /// </summary>
        private Random rnd { get; set; }

        /// <summary>
        /// The channel's ID that the game is in
        /// </summary>
        public ulong ChannelId { get; set; }

        /// <summary>
        /// Has the game started?
        /// </summary>
        public bool hasStarted { get; set; }

        /// <summary>
        /// Whose turn is it?
        /// </summary>
        public int CurrentPlayerIndex { get; set; }

        /// <summary>
        /// What is the current card?
        /// </summary>
        public Card CurrentCard { get; set; }

        /// <summary>
        /// The message that the game is in
        /// </summary>
        public SocketUserMessage GameMessage { get; set; }

        /// <summary>
        /// Timestamp of the last action
        /// </summary>
        public DateTime LastActionTimestamp { get; set; }

        /// <summary>
        /// Some helpful info fo something that recently happened
        /// </summary>
        public string InfoMessage { get; set; }

        /// <summary>
        /// Maximum number of players
        /// </summary>
        public int MaxPlayers = 12;

        /// <summary>
        /// How many cards does the next person have to pick up?
        /// </summary>
        public int StackToPickUp { get; set; }

        public bool isGameOver { get; set; }

        private bool isReversed { get; set; }

        private int turnNumber { get; set; }

        public Game() { }

        public Game(SocketUser host, ulong channelId)
        {
            // Make a new random object
            rnd = new Random();

            // Assign the host
            Host = new Player(host, rnd, this);

            // Initialize player list and add the host
            Players = new List<Player>();
            Players.Add(Host);

            // The host goes first
            CurrentPlayerIndex = 0;

            // First card is random
            CurrentCard = new Card(rnd);

            // We don't want the first card to be a Special
            while (CurrentCard.Special != Special.None)
                CurrentCard = new Card(rnd);

            ChannelId = channelId;

            hasStarted = false;

            InfoMessage = "";

            StackToPickUp = 0;

            isGameOver = false;

            isReversed = false;

            UpdateTimestamp();

            turnNumber = 1;
        }

        /// <summary>
        /// Has this game been inactive for 10 minutes?
        /// </summary>
        public bool isGameInActive() => (DateTime.Now - LastActionTimestamp).TotalMinutes > 10;

        public void UpdateTimestamp() => LastActionTimestamp = DateTime.Now;

        public string ListPlayers(bool highlightCurrent = false, bool listCardCount = true)
        {
            var result = new StringBuilder();

            foreach (var player in Players)
                result.AppendLine($"{(player == Host ? "👑" : "👤")} {player.User.Username} {(listCardCount ? ((player.Deck.Count == 1 ? "**UNO!**" : $"- {player.Deck.Count} lá")) : "")}");

            if (highlightCurrent)
                result.Replace(Players[CurrentPlayerIndex].User.Username, $"**{Players[CurrentPlayerIndex].User.Username}**");

            return result.ToString();
        }

        public async Task UpdateInfoMessage(string message, bool updateGameMessage = false)
        {
            InfoMessage = $"\n\n{message}";

            if (updateGameMessage)
            {
                var currentPlayer = Players[CurrentPlayerIndex];

                var stackText = StackToPickUp > 0 ? $"\n\nPickup Stack: {StackToPickUp}" : "";

                await GameMessage.ModifyAsync(m =>
                {
                    m.Embed = new EmbedBuilder()
                        .WithColor(CurrentCard.GetDiscordColor())
                        .WithAuthor(new EmbedAuthorBuilder()
                            .WithName($"Lượt của {currentPlayer.User.Username} - Lượt #{turnNumber}")
                            .WithIconUrl(currentPlayer.User.GetAvatarUrl() ?? currentPlayer.User.GetDefaultAvatarUrl()))
                        .WithDescription($"Lá bài trước đó: {CurrentCard.ToString()}.{stackText}{InfoMessage}")
                        .WithThumbnailUrl(CurrentCard.GetImageUrl())
                        .WithFields(new EmbedFieldBuilder[]
                        {
                            new EmbedFieldBuilder()
                            {
                                Name = $"Người chơi {(isReversed ? "🔃" : "")}",
                                Value = ListPlayers(true),
                            }
                        })
                        .Build();

                    m.Components = new ComponentBuilder()
                        .WithButton("UNO!", $"sayuno", row: 0, style: ButtonStyle.Secondary, disabled: !Players.Any(p => p.CanSomeoneSayUno))
                        .WithButton("Xem bộ bài của bạn", $"showcardprompt", row: 0, style: ButtonStyle.Secondary)
                        .WithButton("Rời ván", $"leaveduringgame", row: 0, style: ButtonStyle.Secondary)
                        .WithButton("Hủy ván", $"endduringgame", row: 0, style: ButtonStyle.Secondary)
                        .Build();
                });
            }
        }

        /// <summary>
        /// Add a new player to this game
        /// </summary>
        public void AddPlayer(SocketUser user) => Players.Add(new Player(user, rnd, this));

        /// <summary>
        /// Start the game
        /// </summary>
        public async Task DoInitialTurn(SocketMessageComponent command)
        {
            UpdateTimestamp();
            var currentPlayer = Players[CurrentPlayerIndex];
            await command.UpdateAsync(m =>
            {
                m.Embed = new EmbedBuilder()
                    .WithColor(CurrentCard.GetDiscordColor())
                    .WithAuthor(new EmbedAuthorBuilder()
                        .WithName($"Lượt của {currentPlayer.User.Username}")
                        .WithIconUrl(currentPlayer.User.GetAvatarUrl() ?? currentPlayer.User.GetDefaultAvatarUrl()))
                    .WithDescription($"Hiện là lượt của {currentPlayer.User.Username}.\n\n**Bấm nút `Xem bộ bài của bạn` để xem bộ bài bạn đang dùng.**{InfoMessage}")
                    .WithThumbnailUrl(CurrentCard.GetImageUrl())
                    .WithFields(new EmbedFieldBuilder[]
                    {
                        new EmbedFieldBuilder()
                        {
                            Name = "Người chơi",
                            Value = ListPlayers(true),
                        }
                    })
                    .Build();

                m.Components = new ComponentBuilder()
                    .WithButton("UNO!", $"sayuno", row: 0, style: ButtonStyle.Secondary, disabled: true)
                    .WithButton("Xem bộ bài của bạn", $"showcardprompt", row: 0, style: ButtonStyle.Secondary)
                    .WithButton("Rời ván", $"leaveduringgame", row: 0, style: ButtonStyle.Secondary)
                    .WithButton("Hủy ván", $"endduringgame", row: 0, style: ButtonStyle.Secondary)
                    .Build();
            });
        }

        /// <summary>
        /// Have a player play a card
        /// </summary>
        public async Task DoTurn(Card inputCard, bool playedCard = true)
        {
            turnNumber++;
            UpdateTimestamp();

            await CheckForWinner();
            if (isGameOver)
                return;

            var previousPlayer = Players[CurrentPlayerIndex];

            var lastCard = CurrentCard;

            CurrentCard = inputCard;

            // Increment the turn
            if (CurrentCard.Special == Special.Reverse)
            {
                // Reverse's in a 2 player game act like a skip
                // https://twitter.com/realUNOgame/status/1478019270483320839
                if (Players.Count == 2)
                {
                    IncrementTurn();
                    IncrementTurn();
                }
                else
                {
                    isReversed = !isReversed;
                    IncrementTurn();
                }
            }
            else if (CurrentCard.Special == Special.Skip)
            {
                IncrementTurn();
                IncrementTurn();
            }
            else
                IncrementTurn();

            // Add pickup cards to the stack
            if (CurrentCard.Special == Special.WildPlusTwo)
                StackToPickUp += 2;
            else if (CurrentCard.Special == Special.WildPlusFour)
                StackToPickUp += 4;

            // Check if this player has to pick up cards
            if (StackToPickUp > 0 && (lastCard.Special == Special.WildPlusTwo || lastCard.Special == Special.WildPlusFour) && CurrentCard.Special != Special.WildPlusTwo && CurrentCard.Special != Special.WildPlusFour || !playedCard)
            {
                await UpdateInfoMessage($"{previousPlayer.User.Username} phải bốc thêm {StackToPickUp} lá bài 😂🤡");
                await previousPlayer.DrawCards(StackToPickUp);
                StackToPickUp = 0;
            }

            var stackText = StackToPickUp > 0 ? $"\n\nPickup Stack: {StackToPickUp}" : "";

            var currentPlayer = Players[CurrentPlayerIndex];

            // Enable the card buttons on the current player
            await currentPlayer.UpdateCardMenu(null);

            await GameMessage.ModifyAsync(m =>
            {
                m.Embed = new EmbedBuilder()
                    .WithColor(CurrentCard.GetDiscordColor())
                    .WithAuthor(new EmbedAuthorBuilder()
                        .WithName($"Lượt của {currentPlayer.User.Username} - Lượt #{turnNumber}")
                        .WithIconUrl(currentPlayer.User.GetAvatarUrl() ?? currentPlayer.User.GetDefaultAvatarUrl()))
                    .WithDescription(playedCard ? $"{previousPlayer.User.Username} đã dùng {CurrentCard.ToString()}.{stackText}{InfoMessage}" : $"{previousPlayer.User.Username} đã bốc bài.{stackText}{InfoMessage}")
                    .WithThumbnailUrl(CurrentCard.GetImageUrl())
                    .WithFields(new EmbedFieldBuilder[]
                    {
                        new EmbedFieldBuilder()
                        {
                            Name = $"Người chơi {(isReversed ? "🔃" : "")}",
                            Value = ListPlayers(true),
                        }
                    })
                    .Build();

                m.Components = new ComponentBuilder()
                    .WithButton("UNO!", $"sayuno", row: 0, style: ButtonStyle.Secondary, disabled: !Players.Any(p => p.CanSomeoneSayUno))
                    .WithButton("Xem bộ bài của bạn", $"showcardprompt", row: 0, style: ButtonStyle.Secondary)
                    .WithButton("Rời ván", $"leaveduringgame", row: 0, style: ButtonStyle.Secondary)
                    .WithButton("Hủy ván", $"endduringgame", row: 0, style: ButtonStyle.Secondary)
                    .Build();
            });
        }

        /// <summary>
        /// When someone clicks the "Leave Game" button during the game
        /// </summary>
        public async Task RemovePlayerDuringGame(SocketMessageComponent command)
        {
            var player = Players.First(p => p.User.Id == command.User.Id);

            Players.Remove(player);

            await command.UpdateAsync(m =>
            {
                m.Embed = new EmbedBuilder()
                    .WithColor(Colors.Red)
                    .WithDescription("Bạn đã rời ván chơi")
                    .Build();

                m.Components = null;
            });

            await UpdateInfoMessage($"{player.User.Username} vừa rời ván chơi");
            await CheckForWinner();
        }

        /// <summary>
        /// Check if anyone has won the game yet
        /// </summary>
        /// <returns>True if someone has won, false otherwise</returns>
        private async Task CheckForWinner()
        {
            Player winner = null;

            if (Players.Count == 1)
            {
                winner = Players[0];
                isGameOver = true;
            }

            else if (Players.Any(p => p.Deck.Count == 0))
            {
                winner = Players.Where(p => p.Deck.Count == 0).First();
                isGameOver = true;
            }

            if (isGameOver)
            {
                await GameMessage.ModifyAsync(m =>
                {
                    m.Content = "Ván chơi đã kết thúc.";

                    m.Embed = new EmbedBuilder()
                        .WithColor(CurrentCard.GetDiscordColor())
                        .WithAuthor(new EmbedAuthorBuilder()
                            .WithName(winner.User.Username)
                            .WithIconUrl(winner.User.GetAvatarUrl() ?? winner.User.GetDefaultAvatarUrl()))
                        .WithDescription($"{winner.User.Username} đã thắng sau {turnNumber} lượt!{InfoMessage}")
                        .WithThumbnailUrl(CurrentCard.GetImageUrl())
                        .Build();

                    m.Components = null;
                });

                foreach (var player in Players)
                    await player.RemoveAllPlayerCardMenusWithMessage("Ván chơi đã kết thúc.");
            }
        }

        private void IncrementTurn()
        {
            if (isReversed)
            {
                CurrentPlayerIndex--;
                if (CurrentPlayerIndex < 0)
                    CurrentPlayerIndex = Players.Count - 1;
            }
            else
            {
                CurrentPlayerIndex++;
                if (CurrentPlayerIndex >= Players.Count)
                    CurrentPlayerIndex = 0;
            }
        }
    }
}