category: Fixed
- **Forward right after Back no longer loses the opened kilde or variable.** Going Back from a kilde or variable opened from the list and then quickly Forward could leave the reader on the list: the list's own address arrived late and overwrote the entry Forward had just reached. The explorer no longer writes back an address the browser is already showing after Back or Forward.
