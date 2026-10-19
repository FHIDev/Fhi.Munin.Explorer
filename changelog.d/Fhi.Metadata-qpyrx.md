category: Fixed
- **The saved-lists view asks the browser to watch its menus once, not once per overlapping render.** A render that arrived while the first request was still out sent a second one; the browser installed one listener either way, but the extra call made a test fail now and then.
