"""PySide6 GUI for Better RDP.

Thin over :class:`~better_rdp.app.AppService`: this module builds widgets and wires
signals, but every domain action (unlock, list, launch) goes through the service. It is
deliberately not unit-tested — the launch/crypto/rdp contracts are covered by the pytest
suite, and what's left here (real windows, real monitors, real mstsc) is the manual smoke
checklist in docs/SMOKE.md.
"""

from __future__ import annotations

import sys
from pathlib import Path

from PySide6.QtCore import Qt
from PySide6.QtGui import QGuiApplication
from PySide6.QtWidgets import (
    QApplication,
    QCheckBox,
    QComboBox,
    QDialog,
    QDialogButtonBox,
    QFormLayout,
    QHBoxLayout,
    QInputDialog,
    QLabel,
    QLineEdit,
    QListWidget,
    QListWidgetItem,
    QMessageBox,
    QPushButton,
    QSpinBox,
    QVBoxLayout,
    QWidget,
)

from .app import AppService, default_vault_path
from .models import Credential, DisplayMode, DisplayProfile, Server

_MODE_LABELS = {
    DisplayMode.FULLSCREEN_MULTIMON: "Full screen (selected monitors)",
    DisplayMode.WINDOWED_FIXED: "Windowed (fixed resolution)",
    DisplayMode.WINDOWED_DYNAMIC: "Windowed (dynamic resolution)",
}


def unlock(parent=None) -> AppService | None:
    """Prompt for the Master Password and return an unlocked service, or None if the user
    cancelled. Creates the Vault on first run (the same password becomes the set-once key).
    Re-prompts on a wrong password rather than crashing."""
    path = default_vault_path()
    first_run = not path.exists()
    prompt = (
        "Create a Master Password for your new vault:"
        if first_run
        else "Enter your Master Password:"
    )
    while True:
        pw, ok = QInputDialog.getText(
            parent, "Better RDP", prompt, QLineEdit.Password
        )
        if not ok:
            return None
        if not pw:
            continue
        try:
            return AppService.open_or_create(path, pw)
        except Exception:
            prompt = "Wrong Master Password — try again:"
            QMessageBox.warning(
                parent, "Better RDP", "That Master Password did not unlock the vault."
            )


class ServerDialog(QDialog):
    """Add a Server (display name + address)."""

    def __init__(self, parent=None):
        super().__init__(parent)
        self.setWindowTitle("Add server")
        self.name = QLineEdit()
        self.address = QLineEdit()
        self.address.setPlaceholderText("host or host:port, e.g. prod01.intranet.local")
        form = QFormLayout()
        form.addRow("Display name", self.name)
        form.addRow("Address", self.address)
        buttons = QDialogButtonBox(QDialogButtonBox.Ok | QDialogButtonBox.Cancel)
        buttons.accepted.connect(self.accept)
        buttons.rejected.connect(self.reject)
        layout = QVBoxLayout(self)
        layout.addLayout(form)
        layout.addWidget(buttons)

    def result_server(self) -> Server:
        return Server(name=self.name.text().strip(), address=self.address.text().strip())


class CredentialDialog(QDialog):
    """Add a Credential (username, optional domain, password)."""

    def __init__(self, parent=None):
        super().__init__(parent)
        self.setWindowTitle("Add credential")
        self.username = QLineEdit()
        self.domain = QLineEdit()
        self.domain.setPlaceholderText("optional")
        self.password = QLineEdit()
        self.password.setEchoMode(QLineEdit.Password)
        form = QFormLayout()
        form.addRow("Username", self.username)
        form.addRow("Domain", self.domain)
        form.addRow("Password", self.password)
        buttons = QDialogButtonBox(QDialogButtonBox.Ok | QDialogButtonBox.Cancel)
        buttons.accepted.connect(self.accept)
        buttons.rejected.connect(self.reject)
        layout = QVBoxLayout(self)
        layout.addLayout(form)
        layout.addWidget(buttons)

    def result_credential(self) -> Credential:
        username = self.username.text().strip()
        domain = self.domain.text().strip() or None
        return Credential(
            id=username if not domain else f"{domain}\\{username}",
            username=username,
            domain=domain,
            password=self.password.text(),
        )


class ProfileDialog(QDialog):
    """Add a Display Profile: name, mode, and mode-dependent monitor/resolution/scale."""

    def __init__(self, parent=None):
        super().__init__(parent)
        self.setWindowTitle("Add display profile")
        self.name = QLineEdit()
        self.mode = QComboBox()
        for mode, label in _MODE_LABELS.items():
            self.mode.addItem(label, mode)
        self.mode.currentIndexChanged.connect(self._sync_visibility)

        # Monitor checkboxes, enumerated from the physical screens Qt sees. The index used
        # is the Qt screen order; mstsc's selectedmonitors IDs may differ (smoke S2).
        self._monitor_boxes: list[QCheckBox] = []
        self._monitors_row = QWidget()
        mon_layout = QHBoxLayout(self._monitors_row)
        mon_layout.setContentsMargins(0, 0, 0, 0)
        for i, screen in enumerate(QGuiApplication.screens()):
            geo = screen.geometry()
            box = QCheckBox(f"{i}: {geo.width()}x{geo.height()}")
            box.setProperty("monitor_id", i)
            self._monitor_boxes.append(box)
            mon_layout.addWidget(box)
        if self._monitor_boxes:
            self._monitor_boxes[0].setChecked(True)

        self.width = QSpinBox()
        self.width.setRange(640, 7680)
        self.width.setValue(1920)
        self.height = QSpinBox()
        self.height.setRange(480, 4320)
        self.height.setValue(1080)

        self.scale = QComboBox()
        self.scale.addItem("Default", None)
        for s in (100, 125, 150, 175, 200):
            self.scale.addItem(f"{s}%", s)

        self._form = QFormLayout()
        self._form.addRow("Name", self.name)
        self._form.addRow("Mode", self.mode)
        self._form.addRow("Monitors", self._monitors_row)
        self._res_row = QWidget()
        res_layout = QHBoxLayout(self._res_row)
        res_layout.setContentsMargins(0, 0, 0, 0)
        res_layout.addWidget(self.width)
        res_layout.addWidget(QLabel("x"))
        res_layout.addWidget(self.height)
        self._form.addRow("Resolution", self._res_row)
        self._form.addRow("Scale", self.scale)

        buttons = QDialogButtonBox(QDialogButtonBox.Ok | QDialogButtonBox.Cancel)
        buttons.accepted.connect(self.accept)
        buttons.rejected.connect(self.reject)
        layout = QVBoxLayout(self)
        layout.addLayout(self._form)
        layout.addWidget(buttons)
        self._sync_visibility()

    def _sync_visibility(self):
        mode = self.mode.currentData()
        self._monitors_row.setVisible(mode is DisplayMode.FULLSCREEN_MULTIMON)
        self._res_row.setVisible(mode is DisplayMode.WINDOWED_FIXED)

    def result_profile(self) -> DisplayProfile:
        mode = self.mode.currentData()
        monitors = [
            b.property("monitor_id") for b in self._monitor_boxes if b.isChecked()
        ]
        return DisplayProfile(
            name=self.name.text().strip(),
            mode=mode,
            monitors=monitors if mode is DisplayMode.FULLSCREEN_MULTIMON else [],
            width=self.width.value() if mode is DisplayMode.WINDOWED_FIXED else None,
            height=self.height.value() if mode is DisplayMode.WINDOWED_FIXED else None,
            scale_factor=self.scale.currentData(),
        )


class MainWindow(QWidget):
    """Server list is the launch surface; pick a Credential + Profile and Launch."""

    def __init__(self, service: AppService):
        super().__init__()
        self.service = service
        self.setWindowTitle("Better RDP")
        self.resize(560, 420)

        self.servers_list = QListWidget()
        self.servers_list.currentItemChanged.connect(self._on_server_changed)

        self.credential_box = QComboBox()
        self.profile_box = QComboBox()
        self.launch_button = QPushButton("Launch")
        self.launch_button.clicked.connect(self._on_launch)

        add_server_btn = QPushButton("Add server…")
        add_server_btn.clicked.connect(self._add_server)
        add_cred_btn = QPushButton("Add credential…")
        add_cred_btn.clicked.connect(self._add_credential)
        add_profile_btn = QPushButton("Add profile…")
        add_profile_btn.clicked.connect(self._add_profile)

        # Left: server list + its add button.
        left = QVBoxLayout()
        left.addWidget(QLabel("Servers"))
        left.addWidget(self.servers_list, 1)
        left.addWidget(add_server_btn)

        # Right: the launch form for the selected server.
        right = QFormLayout()
        right.addRow("Credential", self.credential_box)
        right.addRow("Display profile", self.profile_box)
        right_box = QVBoxLayout()
        right_box.addLayout(right)
        right_box.addWidget(self.launch_button)
        right_box.addStretch(1)
        right_box.addWidget(add_cred_btn)
        right_box.addWidget(add_profile_btn)

        root = QHBoxLayout(self)
        root.addLayout(left, 1)
        root.addLayout(right_box, 1)

        self._reload()

    # --- data refresh --------------------------------------------------------------

    def _reload(self):
        self._reload_servers()
        self._reload_pickers()
        self._on_server_changed(self.servers_list.currentItem(), None)

    def _reload_servers(self):
        current = self._selected_server_name()
        self.servers_list.clear()
        for server in self.service.servers():
            item = QListWidgetItem(f"{server.name}  ({server.address})")
            item.setData(Qt.UserRole, server.name)
            self.servers_list.addItem(item)
            if server.name == current:
                self.servers_list.setCurrentItem(item)
        if self.servers_list.currentItem() is None and self.servers_list.count():
            self.servers_list.setCurrentRow(0)

    def _reload_pickers(self):
        self.credential_box.clear()
        for cred in self.service.credentials():
            self.credential_box.addItem(cred.id, cred.id)
        self.profile_box.clear()
        for profile in self.service.profiles():
            self.profile_box.addItem(profile.name, profile.name)

    def _on_server_changed(self, current, _previous):
        # Default the pickers to whatever this Server last launched with.
        name = current.data(Qt.UserRole) if current else None
        server = next((s for s in self.service.servers() if s.name == name), None)
        has_launchables = (
            server is not None
            and self.credential_box.count() > 0
            and self.profile_box.count() > 0
        )
        self.launch_button.setEnabled(has_launchables)
        if server is None:
            return
        if server.last_credential_id:
            self._select(self.credential_box, server.last_credential_id)
        if server.last_profile_name:
            self._select(self.profile_box, server.last_profile_name)

    # --- actions -------------------------------------------------------------------

    def _on_launch(self):
        server_name = self._selected_server_name()
        cred_id = self.credential_box.currentData()
        profile_name = self.profile_box.currentData()
        if not (server_name and cred_id and profile_name):
            return
        try:
            self.service.launch(server_name, cred_id, profile_name)
        except Exception as exc:  # surface mstsc/launch failures instead of dying
            QMessageBox.critical(self, "Launch failed", str(exc))
            return
        self._reload_servers()  # last-used may have changed

    def _add_server(self):
        dlg = ServerDialog(self)
        if dlg.exec() == QDialog.Accepted:
            server = dlg.result_server()
            if server.name and server.address:
                self.service.add_server(server)
                self._reload()

    def _add_credential(self):
        dlg = CredentialDialog(self)
        if dlg.exec() == QDialog.Accepted:
            cred = dlg.result_credential()
            if cred.username and cred.password:
                self.service.add_credential(cred)
                self._reload_pickers()
                self._on_server_changed(self.servers_list.currentItem(), None)

    def _add_profile(self):
        dlg = ProfileDialog(self)
        if dlg.exec() == QDialog.Accepted:
            profile = dlg.result_profile()
            if profile.name:
                self.service.add_profile(profile)
                self._reload_pickers()
                self._on_server_changed(self.servers_list.currentItem(), None)

    # --- helpers -------------------------------------------------------------------

    def _selected_server_name(self) -> str | None:
        item = self.servers_list.currentItem()
        return item.data(Qt.UserRole) if item else None

    @staticmethod
    def _select(box: QComboBox, value) -> None:
        idx = box.findData(value)
        if idx >= 0:
            box.setCurrentIndex(idx)


def run(argv: list[str] | None = None) -> int:
    """Entry point: unlock the Vault, then show the main window."""
    app = QApplication(argv if argv is not None else sys.argv)
    service = unlock()
    if service is None:
        return 0
    window = MainWindow(service)
    window.show()
    return app.exec()


if __name__ == "__main__":
    raise SystemExit(run())
