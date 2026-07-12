using System.Diagnostics;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.MessageBox;

namespace Task.Monitor.System.Screens;

public class ScreenApplication
{
    public sealed class ScreenApplicationContext(ISystemTerminal terminal)
    {
        private Stack<Screen> screenStack = new();
        private Lock @lock = new();

        internal Screen? OwnerScreen
        {
            get {
                lock (@lock) {
                    return screenStack.Peek();
                }
            }
            set {
                ArgumentNullException.ThrowIfNull(value, nameof(OwnerScreen));
                lock (@lock) {
                    if (screenStack.Count > 0 && screenStack.Peek() == value) {
                        return;
                    }

                    if (screenStack.Count > 0) {
                        Screen currentScreen = screenStack.Peek();
                        currentScreen.Close();
                    }

                    screenStack.Push(value);
                    Screen currScreen = value;
                    FitScreenToConsole(currScreen);
                    currScreen.Show();
                }
            }
        }

        private void FitScreenToConsole(Screen screen)
        {
            screen.X = 0;
            screen.Y = 0;
            screen.Width = terminal.WindowWidth;
            screen.Height = terminal.WindowHeight;
        }
        
        public void RunApplicationLoop()
        {
            var consoleKey = ConsoleKey.None;
            int screenWidth = terminal.WindowWidth;
            int screenHeight = terminal.WindowHeight;
            bool inResize = false;
            bool running = true;

            // Main application loop.
            // Blocks the main thread and dispatches events to the loaded screen.
            while (running) {
                lock (@lock) {
                    Screen currScreen = screenStack.Peek();

                    // Resize Events.
                    if (screenWidth != terminal.WindowWidth || screenHeight != terminal.WindowHeight) {
                        FitScreenToConsole(currScreen);
                        currScreen.Clear();
                        currScreen.Resize();
                        currScreen.Draw();
                        screenWidth = terminal.WindowWidth;
                        screenHeight = terminal.WindowHeight;
                        inResize = true;
                        continue;
                    }

                    if (inResize) {
                        Thread.Sleep(30);
                        FitScreenToConsole(currScreen);
                        currScreen.Clear();
                        currScreen.Resize();
                        currScreen.Draw();
                        inResize = false;
                    }
                    
                    // Key Events.
                    if (terminal.KeyAvailable) {
                        ConsoleKeyInfo consoleKeyInfo = terminal.ReadKey();
                        consoleKey = consoleKeyInfo.Key;
                        bool handled = false;
                        currScreen.KeyPressed(consoleKeyInfo, ref handled);

                        if (!handled && consoleKey == ConsoleKey.Escape && screenStack.Count == 1) {
                            continue;
                        }

                        if (!handled && (consoleKey == ConsoleKey.Escape || consoleKey == ConsoleKey.F10)) {
                            currScreen.Close();
                            _ = screenStack.Pop();

                            if (screenStack.Count == 0) {
                                running = false;
                                continue;
                            }

                            currScreen = screenStack.Peek();
                            FitScreenToConsole(currScreen);
                            currScreen.Show();

                            continue;
                        }
                    }
                }

                if (terminal.KeyAvailable) {
                    continue;
                }
                
                // Small delay to prevent busy-wait.
                Thread.Sleep(100);
            }

            lock (@lock) {
                while (screenStack.Count > 0) {
                    Screen screen = screenStack.Pop();
                    screen.Close();
                }
            }
        }
    }

    private readonly Dictionary<Type, Screen> registeredScreens;
    private readonly ScreenApplicationContext applicationContext;
    private int invocationCount = 0;

    public ScreenApplication(ISystemTerminal terminal)
    {
        registeredScreens = new Dictionary<Type, Screen>();
        applicationContext = new ScreenApplicationContext(terminal);
    }
    
    public void RegisterScreen(Screen screen)
    {
        if (registeredScreens.ContainsKey(screen.GetType())) {
            throw new InvalidOperationException($"Screen {screen.GetType()} is already registered.");
        }
        
        registeredScreens.Add(screen.GetType(), screen);
    }
    
    public void Run(Screen screen)
    {
        ArgumentNullException.ThrowIfNull(screen, nameof(screen));

        if (++invocationCount > 1) {
            throw new InvalidOperationException("Screen App is already running.");
        }
        
        if (!registeredScreens.ContainsKey(screen.GetType())) {
            throw new InvalidOperationException($"Screen {screen.GetType()} is not registered.");
        }
        
        applicationContext.OwnerScreen = screen;
        applicationContext.RunApplicationLoop();
    }

    public void ShowScreen<T>() where T : Screen
    {
        if (!registeredScreens.ContainsKey(typeof(T))) {
            throw new InvalidOperationException($"Screen {typeof(T)} is not registered.");
        }
        
        Screen screen = (T)registeredScreens[typeof(T)];
        
        applicationContext.OwnerScreen = screen;
    }
}
