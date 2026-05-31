namespace KSPCraftManager
{
    /// <summary>
    /// Interface for all mod subsystem managers.
    /// Provides a consistent lifecycle: Initialize, Shutdown.
    /// </summary>
    public interface IManager
    {
        /// <summary>
        /// Initializes the manager. Called during mod startup in dependency order.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Shuts down the manager. Called during mod shutdown.
        /// </summary>
        void Shutdown();
    }
}
