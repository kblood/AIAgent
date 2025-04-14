using System.Windows.Input;
using System;

namespace AIAgentTest.Commands
{
    /// <summary>
    /// Extension methods for ICommand
    /// </summary>
    public static class CommandExtensions
    {
        /// <summary>
        /// Raises the CanExecuteChanged event if the command supports it
        /// </summary>
        /// <param name="command">The command</param>
        public static void RaiseCanExecuteChanged(this ICommand command)
        {
            if (command is RelayCommand relayCommand)
            {
                relayCommand.RaiseCanExecuteChanged();
            }
            else if (command is RelayCommand<object> genericRelayCommand)
            {
                genericRelayCommand.RaiseCanExecuteChanged();
            }
            // For other command types, this is a no-op
        }
    }
}
