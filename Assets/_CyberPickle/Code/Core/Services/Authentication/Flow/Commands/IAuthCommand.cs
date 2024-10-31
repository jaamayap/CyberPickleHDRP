// File: Assets/_CyberPickle/Code/Core/Services/Authentication/Flow/Commands/IAuthCommand.cs
using System.Threading.Tasks;

namespace CyberPickle.Core.Services.Authentication.Flow.Commands
{
    public interface IAuthCommand
    {
        Task Execute();
        void Undo();
    }
}
