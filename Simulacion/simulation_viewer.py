import tkinter as tk
from tkinter import ttk
import pandas as pd
import os

class SimulationViewer:
    def __init__(self, root):
        self.root = root
        self.root.title("Simulation Viewer")
        
        self.files = self.get_simulation_files()
        self.current_file = None
        self.data = None
        self.current_game = 0
        self.current_turn = 0
        self.turn_duration = 1
        self.playing = False
        self.current_player = None
        self.board = [[[] for _ in range(3)] for _ in range(3)]  # Inicializar el tablero

        self.create_widgets()
        
    def create_widgets(self):
        self.file_label = tk.Label(self.root, text="Select Simulation File")
        self.file_label.pack()

        self.file_listbox = tk.Listbox(self.root, selectmode=tk.SINGLE)
        for file in self.files:
            self.file_listbox.insert(tk.END, file)
        self.file_listbox.pack()
        self.file_listbox.bind('<<ListboxSelect>>', self.load_file)

        self.game_frame = tk.Frame(self.root)
        self.game_frame.pack()

        self.grid_frame = tk.Frame(self.root)
        self.grid_frame.pack()

        self.control_frame = tk.Frame(self.root)
        self.control_frame.pack()

        self.turn_duration_label = tk.Label(self.control_frame, text="Turn Duration (seconds)")
        self.turn_duration_label.grid(row=0, column=0)
        self.turn_duration_spinbox = tk.Spinbox(self.control_frame, from_=1, to=10, increment=1, width=5)
        self.turn_duration_spinbox.grid(row=0, column=1)
        self.turn_duration_spinbox.bind("<FocusOut>", self.update_turn_duration)

        self.pause_button = tk.Button(self.control_frame, text="Pause", command=self.toggle_play)
        self.pause_button.grid(row=0, column=2)

        self.stop_button = tk.Button(self.control_frame, text="Stop", command=self.stop)
        self.stop_button.grid(row=0, column=3)

        self.game_selector_label = tk.Label(self.control_frame, text="Game Number")
        self.game_selector_label.grid(row=1, column=0)
        self.game_selector_spinbox = tk.Spinbox(self.control_frame, from_=1, to=1, width=5, command=self.update_game)
        self.game_selector_spinbox.grid(row=1, column=1)

        self.turn_selector_label = tk.Label(self.control_frame, text="Turn Number")
        self.turn_selector_label.grid(row=2, column=0)
        self.turn_selector_spinbox = tk.Spinbox(self.control_frame, from_=1, to=1, width=5, command=self.update_turn)
        self.turn_selector_spinbox.grid(row=2, column=1)

    def get_simulation_files(self):
        return [file for file in os.listdir() if file.startswith("simulation_results_W") and file.endswith(".csv")]

    def load_file(self, event):
        selection = event.widget.curselection()
        if selection:
            self.current_file = event.widget.get(selection[0])
            self.data = pd.read_csv(self.current_file)
            self.current_game = 0
            self.current_turn = 0
            self.update_game_selector()
            self.update_game_display()

    def update_game_selector(self):
        if self.data is not None:
            total_games = len(self.data)
            self.game_selector_spinbox.config(to=total_games)
            self.game_selector_spinbox.delete(0, tk.END)
            self.game_selector_spinbox.insert(0, 1)

    def update_game_display(self):
        if self.data is not None:
            self.board = [[[] for _ in range(3)] for _ in range(3)]  # Reiniciar el tablero
            self.clear_grid()
            self.show_game_info()
            self.show_turn()

    def show_game_info(self):
        game_info = self.data.iloc[self.current_game]
        winner = game_info['winner']
        turns = game_info['turns']
        moves = game_info['moves'].split(';')
        self.moves = moves

        game_info_label = tk.Label(self.game_frame, text=f"Game {self.current_game + 1} - Winner: {winner} - Total Turns: {turns}")
        game_info_label.pack()

    def show_turn(self):
        if self.current_turn < len(self.moves):
            move = self.moves[self.current_turn]
            self.apply_move(move)
            self.current_turn += 1

    def apply_move(self, move):
        if move[0] == 'W':
            self.current_player = 'W'
        elif move[0] == 'B':
            self.current_player = 'B'

        if move[1] == 'C':
            piece = move[2]
            pos = move[4:-1]
            x, y = map(int, pos.split(','))
            self.board[x][y].append(f"{self.current_player}{piece}")
        elif move[1] == 'M':
            piece = move[2]
            pos_start = move[4:9]
            x1, y1 = map(int, pos_start.split(','))
            pos_end = move[11:-1]
            x2, y2 = map(int, pos_end.split(','))
            self.board[x1][y1].remove(f"{self.current_player}{piece}")
            self.board[x2][y2].append(f"{self.current_player}{piece}")

        self.display_board()

    def display_board(self):
        self.clear_grid()
        board_text = ""
        for row in self.board:
            row_text = ""
            for cell in row:
                if cell:
                    top_piece = cell[-1]
                    row_text += f"{top_piece}|"
                else:
                    row_text += " |"
            row_text = row_text[:-1]  # Remove the last "|"
            board_text += row_text + "\n"
        board_label = tk.Label(self.grid_frame, text=board_text, font=("Courier", 16))
        board_label.pack()

    def clear_grid(self):
        for widget in self.grid_frame.winfo_children():
            widget.destroy()

    def update_turn_duration(self, event):
        try:
            self.turn_duration = int(self.turn_duration_spinbox.get())
        except ValueError:
            self.turn_duration = 1
            self.turn_duration_spinbox.delete(0, tk.END)
            self.turn_duration_spinbox.insert(0, "1")

    def toggle_play(self):
        self.playing = not self.playing
        if self.playing:
            self.pause_button.config(text="Pause")
            self.play_game()
        else:
            self.pause_button.config(text="Play")

    def play_game(self):
        if self.playing and self.current_turn < len(self.moves):
            self.show_turn()
            self.root.after(self.turn_duration * 1000, self.play_game)

    def stop(self):
        self.playing = False
        self.pause_button.config(text="Play")
        self.current_turn = 0
        self.update_game_display()

    def update_game(self):
        self.current_game = int(self.game_selector_spinbox.get()) - 1
        self.current_turn = 0
        self.update_turn_selector()
        self.update_game_display()

    def update_turn_selector(self):
        if self.data is not None:
            total_turns = len(self.moves)
            self.turn_selector_spinbox.config(to=total_turns)
            self.turn_selector_spinbox.delete(0, tk.END)
            self.turn_selector_spinbox.insert(0, 1)

    def update_turn(self):
        self.current_turn = int(self.turn_selector_spinbox.get()) - 1
        self.update_game_display()

if __name__ == "__main__":
    root = tk.Tk()
    app = SimulationViewer(root)
    root.mainloop()
