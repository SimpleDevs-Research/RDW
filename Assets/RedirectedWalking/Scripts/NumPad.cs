using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
using System.Linq;

public class NumPad : MonoBehaviour
{
    [System.Flags]
    public enum NumPadCheck : int {
        None        = 0x00,
        NotEmpty    = 0x01,
        DigitsOnly  = 0x02,
        CharsOnly   = 0x04
    }

    public TMP_InputField inputField;
    public CanvasGroup errorGroup;
    public TextMeshProUGUI errorMessage;
    [Space]
    public NumPadCheck validation = NumPadCheck.None;
    [Space]
    public UnityEvent<string> onEnter;
    public UnityEvent<string> onError;

    public string value => inputField.text;

    public void AddCharacter(string c) {
        inputField.text += c;
        if (errorMessage != null) errorMessage.text = "";
        if (errorGroup != null)   errorGroup.alpha = 0f;
    }
    
    public void DeleteCharacter() {
        if (!string.IsNullOrEmpty(inputField.text)) {
            // Remove 1 character starting from the last index
            inputField.text = inputField.text.Remove(inputField.text.Length - 1, 1);
        }
        if (errorMessage != null) errorMessage.text = "";
        if (errorGroup != null)   errorGroup.alpha = 0f;
    }

    public void Enter() {
        // Check: not empty
        if ((validation & NumPadCheck.NotEmpty) != NumPadCheck.None) {
            if (value.Length == 0) {
                HandleError("Must not be empty");
                return;
            }
        }

        // Check: integers only
        if ((validation & NumPadCheck.DigitsOnly) != NumPadCheck.None) {
            if (!value.All(char.IsDigit)) {
                HandleError("Contains alphabetical characters");
                return;
            }
        }

        // Check: Alphabetical characters only
        if ((validation & NumPadCheck.CharsOnly) != NumPadCheck.None) {
            if (!value.All(char.IsLetter)) {
                HandleError("Contains numerical digits");
                return;
            }
        }

        // Validated characters. We can continue to entering
        onEnter?.Invoke(value);
    }

    public void HandleError(string err) {
        Debug.LogError(err);
        if (errorMessage != null) errorMessage.text = err;
        if (errorGroup != null)   errorGroup.alpha = 1f;
        onError?.Invoke(err);
    }
}
