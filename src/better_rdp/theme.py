"""Visual theme for the GUI: a Fusion base + a dark Qt stylesheet.

Kept apart from gui.py so the widget wiring stays readable and the look can be tweaked in
one place. Palette is a muted "midnight" set (Tokyo-Night-ish) with a green accent on the
primary Launch action, echoing the green prompt accent in the user's shell.
"""

from __future__ import annotations

# --- palette -----------------------------------------------------------------------
BG = "#1a1b26"        # window background
SURFACE = "#1f2335"   # panels / inputs
ELEVATED = "#24283b"  # cards
BORDER = "#2f344d"
BORDER_HI = "#3d4366"
TEXT = "#c0caf5"
MUTED = "#7f88b0"
ACCENT = "#7aa2f7"    # blue — focus / selection
GREEN = "#9ece6a"     # primary action
GREEN_HI = "#b9f27c"
DANGER = "#f7768e"

STYLESHEET = f"""
* {{
    font-family: "Segoe UI", "Inter", sans-serif;
    font-size: 13px;
    color: {TEXT};
}}

QWidget {{
    background-color: {BG};
}}

QDialog {{
    background-color: {BG};
}}

/* Headings ----------------------------------------------------------------------- */
QLabel#header {{
    font-size: 22px;
    font-weight: 700;
    color: {TEXT};
    padding: 2px 0;
}}
QLabel#subtitle {{
    font-size: 12px;
    color: {MUTED};
}}
QLabel#sectionLabel {{
    font-size: 11px;
    font-weight: 700;
    color: {MUTED};
    text-transform: uppercase;
    letter-spacing: 1px;
}}
QLabel {{
    background: transparent;
}}

/* Card container ----------------------------------------------------------------- */
QFrame#card {{
    background-color: {ELEVATED};
    border: 1px solid {BORDER};
    border-radius: 10px;
}}
QFrame#divider {{
    background-color: {BORDER};
    max-height: 1px;
    min-height: 1px;
    border: none;
}}

/* Server list -------------------------------------------------------------------- */
QListWidget {{
    background-color: {SURFACE};
    border: 1px solid {BORDER};
    border-radius: 10px;
    padding: 4px;
    outline: 0;
}}
QListWidget::item {{
    padding: 9px 10px;
    border-radius: 7px;
    margin: 1px 0;
}}
QListWidget::item:hover {{
    background-color: {ELEVATED};
}}
QListWidget::item:selected {{
    background-color: {ACCENT};
    color: #11131c;
}}

/* Inputs ------------------------------------------------------------------------- */
QComboBox, QLineEdit, QSpinBox {{
    background-color: {SURFACE};
    border: 1px solid {BORDER};
    border-radius: 8px;
    padding: 7px 10px;
    selection-background-color: {ACCENT};
    selection-color: #11131c;
}}
QComboBox:hover, QLineEdit:hover, QSpinBox:hover {{
    border: 1px solid {BORDER_HI};
}}
QComboBox:focus, QLineEdit:focus, QSpinBox:focus {{
    border: 1px solid {ACCENT};
}}
QComboBox::drop-down {{
    border: none;
    width: 22px;
}}
QComboBox QAbstractItemView {{
    background-color: {ELEVATED};
    border: 1px solid {BORDER_HI};
    border-radius: 8px;
    padding: 4px;
    selection-background-color: {ACCENT};
    selection-color: #11131c;
    outline: 0;
}}

/* Buttons ------------------------------------------------------------------------ */
QPushButton {{
    background-color: {SURFACE};
    border: 1px solid {BORDER_HI};
    border-radius: 8px;
    padding: 8px 14px;
    color: {TEXT};
}}
QPushButton:hover {{
    background-color: {ELEVATED};
    border: 1px solid {ACCENT};
}}
QPushButton:pressed {{
    background-color: {BORDER};
}}
QPushButton:disabled {{
    color: {MUTED};
    border: 1px solid {BORDER};
    background-color: {BG};
}}

/* Primary action (Launch) */
QPushButton#primary {{
    background-color: {GREEN};
    color: #10210a;
    font-size: 15px;
    font-weight: 700;
    border: none;
    padding: 12px 18px;
}}
QPushButton#primary:hover {{
    background-color: {GREEN_HI};
}}
QPushButton#primary:pressed {{
    background-color: {GREEN};
}}
QPushButton#primary:disabled {{
    background-color: {BORDER};
    color: {MUTED};
}}

/* Subtle "add…" buttons */
QPushButton#ghost {{
    background-color: transparent;
    border: 1px dashed {BORDER_HI};
    color: {MUTED};
}}
QPushButton#ghost:hover {{
    color: {TEXT};
    border: 1px dashed {ACCENT};
    background-color: {SURFACE};
}}

/* Checkboxes --------------------------------------------------------------------- */
QCheckBox {{
    spacing: 7px;
    background: transparent;
}}
QCheckBox::indicator {{
    width: 16px;
    height: 16px;
    border-radius: 4px;
    border: 1px solid {BORDER_HI};
    background-color: {SURFACE};
}}
QCheckBox::indicator:checked {{
    background-color: {GREEN};
    border: 1px solid {GREEN};
}}

QToolTip {{
    background-color: {ELEVATED};
    color: {TEXT};
    border: 1px solid {BORDER_HI};
    padding: 5px 7px;
    border-radius: 6px;
}}

/* Scrollbars --------------------------------------------------------------------- */
QScrollBar:vertical {{
    background: transparent;
    width: 10px;
    margin: 2px;
}}
QScrollBar::handle:vertical {{
    background: {BORDER_HI};
    border-radius: 5px;
    min-height: 24px;
}}
QScrollBar::handle:vertical:hover {{
    background: {ACCENT};
}}
QScrollBar::add-line:vertical, QScrollBar::sub-line:vertical {{
    height: 0;
}}
"""


def apply(app) -> None:
    """Apply the Fusion base style + dark stylesheet to a QApplication."""
    app.setStyle("Fusion")
    app.setStyleSheet(STYLESHEET)
