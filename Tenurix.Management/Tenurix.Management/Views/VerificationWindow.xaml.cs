using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Tenurix.Management.Client.Api;

namespace Tenurix.Management.Views;

public partial class VerificationWindow : Window
{
    private readonly TenurixApiClient _api;
    private readonly string _email;
    private readonly string _password;
    private readonly TextBox[] _codeBoxes;

    private DispatcherTimer? _timer;
    private int _secondsRemaining;

    // Attempt limits
    private int _verifyAttempts;
    private const int MaxVerifyAttempts = 3;
    private int _resendCount;
    private const int MaxResends = 2;
    private bool _isBlocked;

    public VerificationWindow(TenurixApiClient api, string email, string password, string maskedEmail)
    {
        InitializeComponent();

        _api = api;
        _email = email;
        _password = password;
        _codeBoxes = new[] { Code1, Code2, Code3, Code4, Code5, Code6 };

        EmailDisplay.Text = maskedEmail;
        UpdateAttemptsLeft();

        StartResendTimer();

        Loaded += (_, _) => Code1.Focus();
    }

    private void StartResendTimer()
    {
        _secondsRemaining = 120; // 2 minutes
        ResendBtn.Visibility = Visibility.Collapsed;
        ResendLabel.Visibility = Visibility.Visible;
        TimerText.Visibility = Visibility.Visible;

        _timer?.Stop();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) =>
        {
            _secondsRemaining--;
            if (_secondsRemaining <= 0)
            {
                _timer.Stop();
                TimerText.Visibility = Visibility.Collapsed;
                ResendLabel.Visibility = Visibility.Collapsed;

                // Only show resend button if under limit and not blocked
                if (_resendCount < MaxResends && !_isBlocked)
                    ResendBtn.Visibility = Visibility.Visible;
            }
            else
            {
                var min = _secondsRemaining / 60;
                var sec = _secondsRemaining % 60;
                TimerText.Text = $"{min}:{sec:D2}";
            }
        };
        _timer.Start();
    }

    private void StartBlockTimer(int blockSeconds)
    {
        _isBlocked = true;
        _secondsRemaining = blockSeconds;

        // Disable everything
        VerifyBtn.IsEnabled = false;
        ResendBtn.Visibility = Visibility.Collapsed;
        foreach (var box in _codeBoxes)
            box.IsEnabled = false;

        // Show block countdown
        ResendLabel.Visibility = Visibility.Visible;
        ResendLabel.Text = "Too many attempts. Try again in ";
        TimerText.Visibility = Visibility.Visible;
        AttemptsText.Visibility = Visibility.Collapsed;

        _timer?.Stop();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) =>
        {
            _secondsRemaining--;
            if (_secondsRemaining <= 0)
            {
                _timer.Stop();
                _isBlocked = false;

                // Reset attempts
                _verifyAttempts = 0;
                _resendCount = 0;

                // Re-enable
                VerifyBtn.IsEnabled = true;
                foreach (var box in _codeBoxes)
                    box.IsEnabled = true;

                // Clear and refocus
                foreach (var box in _codeBoxes)
                    box.Text = "";
                _codeBoxes[0].Focus();

                ErrorText.Text = "";
                ResendLabel.Text = "Resend code in ";
                UpdateAttemptsLeft();
                StartResendTimer();
            }
            else
            {
                var min = _secondsRemaining / 60;
                var sec = _secondsRemaining % 60;
                TimerText.Text = $"{min}:{sec:D2}";
            }
        };
        _timer.Start();
    }

    private void UpdateAttemptsLeft()
    {
        var left = MaxVerifyAttempts - _verifyAttempts;
        if (left < MaxVerifyAttempts && left > 0)
        {
            AttemptsText.Text = $"{left} attempt{(left == 1 ? "" : "s")} remaining";
            AttemptsText.Visibility = Visibility.Visible;
        }
        else if (left <= 0)
        {
            AttemptsText.Text = "No attempts remaining";
            AttemptsText.Visibility = Visibility.Visible;
        }
        else
        {
            AttemptsText.Visibility = Visibility.Collapsed;
        }
    }

    private void Code_TextChanged(object sender, TextChangedEventArgs e)
    {
        var box = (TextBox)sender;

        // Only allow digits
        if (box.Text.Length > 0 && !char.IsDigit(box.Text[0]))
        {
            box.Text = "";
            return;
        }

        // Auto-advance to next box
        if (box.Text.Length == 1)
        {
            var idx = Array.IndexOf(_codeBoxes, box);
            if (idx < _codeBoxes.Length - 1)
                _codeBoxes[idx + 1].Focus();
        }
    }

    private void Code_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var box = (TextBox)sender;
        var idx = Array.IndexOf(_codeBoxes, box);

        if (e.Key == Key.Back && string.IsNullOrEmpty(box.Text) && idx > 0)
        {
            _codeBoxes[idx - 1].Focus();
            _codeBoxes[idx - 1].Text = "";
            e.Handled = true;
        }
        else if (e.Key == Key.Left && idx > 0)
        {
            _codeBoxes[idx - 1].Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Right && idx < _codeBoxes.Length - 1)
        {
            _codeBoxes[idx + 1].Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            Verify_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }

        // Handle paste (Ctrl+V)
        if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            var clipText = Clipboard.GetText().Trim();
            PasteCode(clipText);
        }
    }

    private void PasteCode(string text)
    {
        var digits = new string(text.Where(char.IsDigit).ToArray());
        for (int i = 0; i < Math.Min(digits.Length, 6); i++)
        {
            _codeBoxes[i].Text = digits[i].ToString();
        }
        if (digits.Length >= 6)
            _codeBoxes[5].Focus();
    }

    private void Code_GotFocus(object sender, RoutedEventArgs e)
    {
        var box = (TextBox)sender;
        box.SelectAll();
    }

    private async void Verify_Click(object sender, RoutedEventArgs e)
    {
        if (_isBlocked) return;

        var code = string.Concat(_codeBoxes.Select(b => b.Text));

        if (code.Length != 6)
        {
            ErrorText.Text = "Please enter the complete 6-digit code.";
            return;
        }

        ErrorText.Text = "";
        VerifyBtn.IsEnabled = false;
        VerifyBtn.Content = "Verifying...";

        try
        {
            var session = await _api.VerifyTwoFactorAsync(_email, _password, code);

            _api.SetToken(session.Token);

            var shell = new ShellWindow(_api, session);
            shell.Show();

            _timer?.Stop();
            this.Close();
        }
        catch (Exception ex)
        {
            _verifyAttempts++;
            UpdateAttemptsLeft();

            if (_verifyAttempts >= MaxVerifyAttempts)
            {
                ErrorText.Text = "Too many failed attempts. Please wait before trying again.";
                StartBlockTimer(300); // Block for 5 minutes
                return;
            }

            ErrorText.Text = ex.Message;

            // Clear the code boxes on error
            foreach (var box in _codeBoxes)
                box.Text = "";
            _codeBoxes[0].Focus();
        }
        finally
        {
            if (!_isBlocked)
            {
                VerifyBtn.IsEnabled = true;
            }
            VerifyBtn.Content = "Verify";
        }
    }

    private async void Resend_Click(object sender, RoutedEventArgs e)
    {
        if (_isBlocked) return;

        _resendCount++;

        ResendBtn.IsEnabled = false;
        ResendBtn.Content = "Sending...";

        try
        {
            await _api.ResendTwoFactorAsync(_email, _password);

            // Clear old code
            foreach (var box in _codeBoxes)
                box.Text = "";
            _codeBoxes[0].Focus();

            // Reset verify attempts on resend (new code)
            _verifyAttempts = 0;
            UpdateAttemptsLeft();
            ErrorText.Text = "";

            if (_resendCount >= MaxResends)
            {
                // No more resends — show message and block
                ErrorText.Text = "Maximum resend limit reached. This is your last code.";
                ResendBtn.Visibility = Visibility.Collapsed;
                StartResendTimer();
            }
            else
            {
                StartResendTimer();
            }
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
        }
        finally
        {
            ResendBtn.IsEnabled = true;
            ResendBtn.Content = "Resend Code";
        }
    }

    private void BackToLogin_Click(object sender, RoutedEventArgs e)
    {
        _timer?.Stop();

        var login = new LoginWindow();
        login.Show();
        this.Close();
    }
}
