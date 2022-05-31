using Discord.WebSocket;
using UNO.Types;

namespace UNO
{
    public static class GameUtilities
    {
        /// <summary>
        /// Print an error and return an empty RetrieveGame object
        /// </summary>
        private static async Task<RetrievedGame> FailToFindAGameWithPlayer(this SocketInteraction interaction, string error)
        {
            await interaction.PrintError(error);
            return new RetrievedGame();
        }

        /// <summary>
        /// Searches the current channel for a game, makes sure the commanding user is in it, and that the game has started
        /// </summary>
        public static async Task<RetrievedGame> TryToFindGameInThisChannelWithUser(this SocketInteraction command, List<Game> activeGames)
        {
            // Check if there's a game in this channel
            if (!activeGames.Any(g => g.ChannelId == command.Channel.Id))
                return await command.FailToFindAGameWithPlayer("Channel này hiện không có ván chơi nào.");

            // Get the game object
            var retrievedGame = new RetrievedGame(activeGames.Where(g => g.ChannelId == command.Channel.Id).First());

            // Check if the commanding user is in this game
            if (!retrievedGame.Game.Players.Any(p => p.User.Id == command.User.Id))
                return await command.FailToFindAGameWithPlayer("Bạn hiện không tham gia ván chơi trong channel này.");

            // Check if the game has started yet
            else if (!retrievedGame.Game.hasStarted)
                return await command.FailToFindAGameWithPlayer("Ván chơi chưa bắt đầu.");

            // The player is in this game and it's started
            // We're good to go
            retrievedGame.SetPlayer(retrievedGame.Game.Players.First(p => p.User.Id == command.User.Id));

            return retrievedGame;
        }
    }
}