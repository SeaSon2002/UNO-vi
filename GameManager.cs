using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;

namespace UNO
{
    public class GameManager
    {
        public List<Types.Game> ActiveGames;

        public GameManager() => ActiveGames = new List<Types.Game>();

        /// <summary>
        /// Check if the user is already playing a game, or if they are hosting one already, or if there isn't one in this channel
        /// </summary>
        private async Task<bool> CanWeStartANewGame(SocketInteraction command)
        {
            // Check if there isn't an active game in this channel already
            if (ActiveGames.Any(g => g.ChannelId == command.Channel.Id))
            {
                var game = ActiveGames.Where(g => g.ChannelId == command.Channel.Id).First();

                // Check if the game is old
                if (game.isGameInActive())
                {
                    ActiveGames.Remove(game);
                    return true;
                }

                // Check if the game is over
                if (game.isGameOver)
                {
                    ActiveGames.Remove(game);
                    return true;
                }

                await command.PrintError($"Channel này đang có một ván chơi, hãy chờ đến khi nó kết thúc hoặc khi {game.Host.User.Mention} hủy ván hiện tại.");
                return false;
            }

            // Check if they're hosting any games
            if (ActiveGames.Any(g => g.Host.User.Id == command.User.Id))
            {
                var game = ActiveGames.Where(g => g.Host.User.Id == command.User.Id).First();

                // Check if the game is old
                if (game.isGameInActive())
                {
                    ActiveGames.Remove(game);
                    return true;
                }

                // Check if the game is over
                if (game.isGameOver)
                {
                    ActiveGames.Remove(game);
                    return true;
                }

                await command.PrintError("Bạn đang là chủ ván chơi. Hãy hoàn thành hoặc hủy ván trước.\n\nBấm nút \"Hủy ván\" để hủy ván của bạn.");
                return false;
            }

            // Check if they're playing any games
            if (ActiveGames.Any(g => g.Players.Any(p => p.User.Id == command.User.Id)))
            {
                var game = ActiveGames.Where(g => g.Host.User.Id == command.User.Id).First();

                // Check if the game is old
                if (game.isGameInActive())
                {
                    ActiveGames.Remove(game);
                    return true;
                }

                // Check if the game is over
                if (game.isGameOver)
                {
                    ActiveGames.Remove(game);
                    return true;
                }

                await command.PrintError("Bạn đang chơi dở ván khác. Hãy hoàn thành hoặc rời ván đó.\n\nBấm nút \"Rời ván\" để rời ván đang tham gia.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Initialize a new game
        /// </summary>
        public async Task TryToInitializeGame(SocketInteraction command)
        {
            // Check if they are able to host a new game
            if (!(await CanWeStartANewGame(command)))
                return;

            var game = new Types.Game(command.User, command.Channel.Id);

            await command.RespondAsync("Đã mở ván chơi mới", embed: new EmbedBuilder()
                    .WithColor(Colors.Red)
                    .WithAuthor(new EmbedAuthorBuilder()
                        .WithName("UNO"))
                    .WithDescription($"{game.Host.User.Username} đã mở ván UNO mới! Bấm nút để tham gia!\n\n{game.ListPlayers(listCardCount: false)}")
                    .Build(),
                components: new ComponentBuilder()
                    .WithButton("Bắt đầu", $"start-{command.User.Id}", row: 0, style: ButtonStyle.Secondary, disabled: true)
                    .WithButton("Hủy ván", $"cancel-{command.User.Id}", row: 0, style: ButtonStyle.Secondary)
                    .WithButton("Tham gia", $"join-{command.User.Id}", row: 1, style: ButtonStyle.Secondary)
                    .WithButton("Rời ván", $"leave-{command.User.Id}", row: 1, style: ButtonStyle.Secondary)
                    .Build());

            ActiveGames.Add(game);
        }

        /// <summary>
        /// Try to join a game
        /// </summary>
        public async Task TryToJoinGame(SocketMessageComponent command, ulong hostId)
        {
            // Check if that game is still valid
            if (!ActiveGames.Any(g => g.Host.User.Id == hostId))
            {
                await command.PrintError("Ván này không tồn tại hoặc đã kết thúc.");
                return;
            }

            // Get the game
            var game = ActiveGames.Where(g => g.Host.User.Id == hostId).First();

            // Check if this game has started already
            if (game.hasStarted)
            {
                await command.PrintError("Ván này đã bắt đầu");
                return;
            }

            // Check if the user is already in the game
            else if (game.Players.Any(p => p.User.Id == command.User.Id))
            {
                await command.PrintError("Bạn đã đang tham gia ván này. Bấm nút \'Rời ván\" nếu không muốn chơi nữa.");
                return;
            }

            // Check if the user trying to join is the host
            else if (game.Host.User.Id == command.User.Id)
            {
                await command.PrintError("Nút này chỉ dành cho những người muốn tham gia vào ván chơi của bạn.");
                return;
            }

            // Check if the game already has 4 players
            else if (game.Players.Count >= game.MaxPlayers)
            {
                await command.PrintError("Số người đã đạt tối đa.");
                return;
            }

            game.AddPlayer(command.User);

            // Update the player list
            await command.UpdateAsync(m =>
            {
                m.Embed = new EmbedBuilder()
                    .WithColor(Colors.Red)
                    .WithAuthor(new EmbedAuthorBuilder()
                        .WithName("UNO"))
                    .WithDescription($"{game.Host.User.Username} đã mở ván UNO mới! Bấm nút để tham gia!\n\n{game.ListPlayers(listCardCount: false)}\n\n*{command.User.Username} vừa tham gia ván chơi*")
                    .Build();

                m.Components = new ComponentBuilder()
                    .WithButton("Bắt đầu", $"start-{game.Host.User.Id}", row: 0, style: ButtonStyle.Secondary, disabled: game.Players.Count == 0)
                    .WithButton("Hủy ván", $"cancel-{game.Host.User.Id}", row: 0, style: ButtonStyle.Secondary)
                    .WithButton("Tham gia", $"join-{game.Host.User.Id}", row: 1, style: ButtonStyle.Secondary, disabled: game.Players.Count >= game.MaxPlayers)
                    .WithButton("Rời ván", $"leave-{game.Host.User.Id}", row: 1, style: ButtonStyle.Secondary)
                    .Build();
            });

            game.UpdateTimestamp();
        }

        /// <summary>
        /// Try to leave a game
        /// </summary>
        public async Task TryToLeaveGame(SocketMessageComponent command, ulong hostId)
        {
            // Check if that game is still valid
            if (!ActiveGames.Any(g => g.Host.User.Id == hostId))
            {
                await command.PrintError("Ván này không tồn tại hoặc đã kết thúc.");
                return;
            }

            // Get the game
            var game = ActiveGames.Where(g => g.Host.User.Id == hostId).First();

            // Check if the user is the host
            if (game.Host.User.Id == command.User.Id)
            {
                await command.PrintError("Bạn là chủ ván. Nếu muốn hủy ván hãy bấm \"Hủy ván\".");
                return;
            }

            // Check if the user is actually in the game
            else if (!game.Players.Any(p => p.User.Id == command.User.Id))
            {
                await command.PrintError("Bạn không tham gia ván này.");
                return;
            }

            // Remove the player
            game.Players.Remove(game.Players.Where(p => p.User.Id == command.User.Id).First());

            // Update the player list
            await command.UpdateAsync(m =>
            {
                m.Embed = new EmbedBuilder()
                    .WithColor(Colors.Red)
                    .WithAuthor(new EmbedAuthorBuilder()
                        .WithName("UNO"))
                    .WithDescription($"{game.Host.User.Username} đã mở ván UNO mới! Bấm nút để tham gia!\n\n{game.ListPlayers(listCardCount: false)}\n\n*{command.User.Username} vừa rời ván chơi*")
                    .Build();

                m.Components = new ComponentBuilder()
                    .WithButton("Bắt đầu", $"start-{game.Host.User.Id}", row: 0, style: ButtonStyle.Secondary, disabled: game.Players.Count == 0)
                    .WithButton("Hủy ván", $"cancel-{game.Host.User.Id}", row: 0, style: ButtonStyle.Secondary)
                    .WithButton("Tham gia", $"join-{game.Host.User.Id}", row: 1, style: ButtonStyle.Secondary, disabled: game.Players.Count >= game.MaxPlayers)
                    .WithButton("Rời ván", $"leave-{game.Host.User.Id}", row: 1, style: ButtonStyle.Secondary)
                    .Build();
            });

            game.UpdateTimestamp();
        }

        /// <summary>
        /// Cancel the game creation
        /// </summary>
        public async Task TryToCancelGame(SocketMessageComponent command, ulong hostId)
        {
            var canCancel = true;

            // Check if that game is still valid
            if (!ActiveGames.Any(g => g.Host.User.Id == hostId))
            {
                await command.PrintError("Ván này không tồn tại hoặc đã kết thúc.");
                canCancel = false;
            }

            // Get the game
            var game = ActiveGames.Where(g => g.Host.User.Id == hostId).First();

            if (game.Host.User.Id != command.User.Id)
            {
                await command.PrintError("Bạn không phải chủ ván. Nếu muốn rời ván hãy bấm \"Rời ván\".");
                canCancel = false;
            }

            if (!canCancel)
                return;

            // Update the player list
            await command.UpdateAsync(m =>
            {
                m.Embed = new EmbedBuilder()
                    .WithColor(Colors.Red)
                    .WithAuthor(new EmbedAuthorBuilder()
                        .WithName("UNO"))
                    .WithDescription($"{game.Host.User.Username} đã hủy ván chơi.\n\nDùng lệnh `/uno` để tạo ván mới trong channel này.")
                    .Build();

                m.Components = null;
            });

            // Remove the game
            ActiveGames.Remove(ActiveGames.Where(g => g.Host.User.Id == command.User.Id).First());
        }

        /// <summary>
        /// Start the game
        /// </summary>
        public async Task TryToStartGame(SocketMessageComponent command, ulong hostId)
        {
            // Check if that game is still valid
            if (!ActiveGames.Any(g => g.Host.User.Id == hostId))
            {
                await command.PrintError("Ván này không tồn tại hoặc đã kết thúc.");
                return;
            }

            // Get the game
            var game = ActiveGames.Where(g => g.Host.User.Id == hostId).First();

            if (game.Host.User.Id != command.User.Id)
            {
                await command.PrintError("Chỉ chủ ván mới có thể bắt đầu.");
                return;
            }

            game.hasStarted = true;

            game.GameMessage = command.Message;

            await game.DoInitialTurn(command);
        }

        /// <summary>
        /// Try to play a card during the game
        /// </summary>
        public async Task TryToPlayCard(SocketMessageComponent command, ulong hostId, string color, string number, string special, int index)
        {
            // Try to find a valid game in this channel with this suer
            var retrievedGame = await command.TryToFindGameInThisChannelWithUser(ActiveGames);

            if (!retrievedGame.hasValidGameAndPlayer)
                return;

            var inputCard = new Types.Card(color, number, special);

            // Check if it's this player's turn
            if (!await retrievedGame.Player.CheckIfItsMyTurn(command))
                return;

            // Check if this card be played
            if (!retrievedGame.Player.CheckIfCardCanBePlayed(inputCard))
            {
                await retrievedGame.Player.UpdateCardMenu(command, "Không thể dùng lá bài đó, hãy dùng lá khác.");
                return;
            }

            // If it's a Wild card, then show the menu to select a color
            if (inputCard.Special == Types.Special.Wild || inputCard.Special == Types.Special.WildPlusFour)
            {
                // Show the wild card menu
                await retrievedGame.Player.ShowWildMenu(command, (Types.Special)Enum.Parse(typeof(Types.Special), special), index);
                return;
            }

            // Play the card
            await retrievedGame.Player.PlayCard(command, inputCard, index);
        }

        /// <summary>
        /// Try to draw a card
        /// </summary>
        public async Task TryToDrawCard(SocketMessageComponent command)
        {
            // Try to find a valid game in this channel with this suer
            var retrievedGame = await command.TryToFindGameInThisChannelWithUser(ActiveGames);

            if (!retrievedGame.hasValidGameAndPlayer)
                return;

            // Check if it's this player's turn
            if (!await retrievedGame.Player.CheckIfItsMyTurn(command))
                return;

            // Have them draw a card
            await retrievedGame.Player.DrawCard(command);
        }

        /// <summary>
        /// Try to play a wild card
        /// </summary>
        public async Task TryToPlayWildCard(SocketMessageComponent command, string color, string special, int index)
        {
            // Try to find a valid game in this channel with this suer
            var retrievedGame = await command.TryToFindGameInThisChannelWithUser(ActiveGames);

            if (!retrievedGame.hasValidGameAndPlayer)
                return;

            // Check if it's this player's turn
            if (!await retrievedGame.Player.CheckIfItsMyTurn(command))
                return;

            var args = command.Data.CustomId.Split("-");

            var inputCard = new Types.Card(args[1], "", args[2]);

            // Check if this card be played
            if (!retrievedGame.Player.CheckIfCardCanBePlayed(inputCard))
            {
                await retrievedGame.Player.UpdateCardMenu(command, "Không thể dùng lá bài đó, hãy dùng lá khác.");
                return;
            }

            // Play the card
            await retrievedGame.Player.PlayCard(command, inputCard, Convert.ToInt32(args[3]));
        }

        /// <summary>
        /// Try to show a wild card menu
        /// </summary>
        public async Task TryToCancelWildMenu(SocketMessageComponent command)
        {
            // Try to find a valid game in this channel with this suer
            var retrievedGame = await command.TryToFindGameInThisChannelWithUser(ActiveGames);

            if (!retrievedGame.hasValidGameAndPlayer)
                return;

            // Check if it's this player's turn
            if (!await retrievedGame.Player.CheckIfItsMyTurn(command))
                return;

            // Show their regular cards
            await retrievedGame.Player.UpdateCardMenu(command);
        }

        /// <summary>
        /// "Leave Game" button (during the game)
        /// </summary>
        public async Task TryToLeaveDuringGame(SocketMessageComponent command)
        {
            // Try to find a valid game in this channel with this suer
            var retrievedGame = await command.TryToFindGameInThisChannelWithUser(ActiveGames);

            if (!retrievedGame.hasValidGameAndPlayer)
                return;

            // Remove the player
            await retrievedGame.Game.RemovePlayerDuringGame(command);

            if (retrievedGame.Game.isGameOver)
                ActiveGames.Remove(retrievedGame.Game);
        }

        /// <summary>
        /// "End Game" button (during the game)
        /// </summary>
        public async Task TryToEndDuringGame(SocketMessageComponent command)
        {
            // Try to find a valid game in this channel with this suer
            var retrievedGame = await command.TryToFindGameInThisChannelWithUser(ActiveGames);

            if (!retrievedGame.hasValidGameAndPlayer)
                return;

            // Check if they're host
            if (retrievedGame.Game.Host.User.Id != retrievedGame.Player.User.Id)
            {
                await command.PrintError($"Chỉ chủ ván ({retrievedGame.Game.Host.User.Username}) mới có thể hủy ván chơi.");
                return;
            }

            // End the game
            await command.UpdateAsync(m =>
            {
                m.Embed = new EmbedBuilder()
                    .WithColor(Colors.Red)
                    .WithAuthor(new EmbedAuthorBuilder()
                        .WithName($"UNO"))
                    .WithDescription($"{retrievedGame.Game.Host.User.Username} đã hủy ván chơi.\n\nDùng lệnh `/uno` để tạo ván mới trong channel này.")
                    .Build();

                m.Components = null;
            });

            ActiveGames.Remove(retrievedGame.Game);
        }

        /// <summary>
        /// Try to show a card menu
        /// </summary>
        public async Task TryToShowCardMenu(SocketMessageComponent command)
        {
            // Try to find a valid game in this channel with this suer
            var retrievedGame = await command.TryToFindGameInThisChannelWithUser(ActiveGames);

            if (!retrievedGame.hasValidGameAndPlayer)
                return;

            // Show their regular cards
            await retrievedGame.Player.UpdateCardMenu(command);
        }

        /// <summary>
        /// /admin reset
        /// </summary>
        public async Task TryToResetGame(SocketSlashCommand command)
        {
            // Has to be an admin
            if (!((SocketGuildUser)command.User).GuildPermissions.Administrator)
            {
                await command.PrintError("Chỉ Admin mới có thể dùng lệnh này.");
                return;
            }

            // Try to find a valid game in this channel
            if (!ActiveGames.Any(g => g.ChannelId == command.Channel.Id))
            {
                await command.PrintError("Channel này hiện không có ván chơi nào.");
                return;
            }

            var game = ActiveGames.Where(g => g.ChannelId == command.Channel.Id).First();

            // Update the game message
            await game.GameMessage.ModifyAsync(m =>
            {
                m.Embed = new EmbedBuilder()
                    .WithColor(Colors.Red)
                    .WithAuthor(new EmbedAuthorBuilder()
                        .WithName($"UNO"))
                    .WithDescription($"{command.User.Username} đã reset ván chơi này.")
                    .Build();

                m.Components = null;
            });

            // Delete the game
            ActiveGames.Remove(game);
            foreach (var player in game.Players)
                await player.RemoveAllPlayerCardMenusWithMessage($"{command.User.Username} đã buộc dừng ván chơi.\n\nDùng lệnh `/uno` để tạo ván mới trong channel này.");

            // Respond to the interaction
            await command.RespondAsync(embed: new EmbedBuilder()
                .WithColor(Colors.Red)
                .WithAuthor(new EmbedAuthorBuilder()
                    .WithName($"UNO"))
                .WithDescription($"{command.User.Username} đã buộc dừng ván chơi.\n\nDùng lệnh `/uno` để tạo ván mới trong channel này.")
                .Build());

            ActiveGames.Remove(game);
        }

        /// <summary>
        /// "UNO!" button
        /// </summary>
        public async Task TryToSayUno(SocketMessageComponent command)
        {
            // Try to find a valid game in this channel with this suer
            var retrievedGame = await command.TryToFindGameInThisChannelWithUser(ActiveGames);

            if (!retrievedGame.hasValidGameAndPlayer)
                return;

            // See if anyone has two cards
            if (!retrievedGame.Game.Players.Any(p => p.CanSomeoneSayUno))
            {
                await command.PrintError("Bạn chậm quá! Đã có người `UNO!` trước bạn 🐢🐢");
                return;
            }

            // Someone said UNO! successfully, who was it?
            var playerWithOneCard = retrievedGame.Game.Players.Where(p => p.CanSomeoneSayUno).First();

            // WOO! They're safe 😎
            if (playerWithOneCard.User.Id == command.User.Id)
            {
                playerWithOneCard.CanSomeoneSayUno = false;
                await retrievedGame.Game.UpdateInfoMessage($"{command.User.Username} đã `UNO!` đầu tiên nên không bị bốc thêm bài.", true);
                await command.PrintSuccess("Chúc mừng, bạn là người đầu tiên `UNO!` nên bạn không bị bốc thêm bài.");
                return;
            }

            // Uh oh... someone has to pick up 2 cards.. 🤡🤡
            await playerWithOneCard.DrawCards(2);

            await retrievedGame.Game.UpdateInfoMessage($"{command.User.Username} đã `UNO!` trước, {playerWithOneCard.User.Username} buộc phải bốc thêm 2 lá bài.", true);

            await command.PrintSuccess($"Bạn đã `UNO!` trước, {playerWithOneCard.User.Username} phải bốc thêm 2 lá bài.");
        }
    }
}