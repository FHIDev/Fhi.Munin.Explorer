category: Notes for hosts

- **The sample page now fills the window and sits on helsedata's own ground colour** - it capped
  itself at 1600px and painted the body pure white, so the sample looked narrower and flatter than
  the page it stands in for. helsedata sets no width cap at all, and its body is `#f6f7fc`, a faint
  blue-grey that is the reason their white cards read as raised rather than as part of the page.
  The tint was already in this stylesheet as `--grey10` and simply was not being used for the body.
  Nothing in the package changed; both are the host's ground to set.
