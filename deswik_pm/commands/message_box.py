"""MessageBox process map command.

Observed payload (ARG_SEP-delimited, in order):
    message, title, icon, <unknown>, buttons, timeout_enabled, timeout_seconds, default_button

Icon values (from help docs): Error/Stop, Exclamation/Warning,
Asterisk/Information, Question, None.
Buttons values: OK, OKCancel, YesNo, YesNoCancel, ...
"""
from .base import DelimitedCommand


class MessageBox(DelimitedCommand):
    """Show a message box dialog when the node runs. VERIFIED."""

    name = "MessageBox"
    fields = [
        "message",
        "title",
        "icon",
        "unknown",
        "buttons",
        "timeout_enabled",
        "timeout_seconds",
        "default_button",
    ]

    def __init__(self, message="", title="", icon="Asterisk", buttons="OK",
                 timeout_enabled=False, timeout_seconds=0, default_button="OK", unknown=""):
        super().__init__(
            message=message, title=title, icon=icon, unknown=unknown,
            buttons=buttons, timeout_enabled=timeout_enabled,
            timeout_seconds=timeout_seconds, default_button=default_button,
        )
