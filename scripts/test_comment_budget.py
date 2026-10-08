import contextlib
import importlib.util
import io
import os
import pathlib
import subprocess
import tempfile
import unittest

_spec = importlib.util.spec_from_file_location("comment_budget", pathlib.Path(__file__).with_name("comment-budget.py"))
cb = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(cb)

ESSAY = "// a\n// b\n// c\n// d\n"
DOC = "    /// <summary>\n    /// a\n    /// b\n    /// c\n    /// d\n    /// </summary>\n"


class ClassifyTests(unittest.TestCase):
    def test_xml_tag_lines_do_not_count_as_text(self):
        kinds = cb.classify(["/// <summary>", "/// Fails closed.", "/// </summary>", "void M();"])
        self.assertEqual(kinds, ["tag", "text", "tag", "code"])

    def test_single_line_member_tags_do_not_count(self):
        kinds = cb.classify(['/// <param name="x">Seconds, never null.</param>', "/// <inheritdoc/>"])
        self.assertEqual(kinds, ["tag", "tag"])

    def test_code_after_block_comment_is_code(self):
        self.assertEqual(cb.classify(["/* why */ cache.Clear();"]), ["code"])
        self.assertEqual(cb.classify(["/*", " * why", " */ cache.Clear();"]), ["tag", "text", "code"])

    def test_glob_in_string_does_not_open_block(self):
        self.assertEqual(cb.classify(['var g = "src/*.cs";', "// a"]), ["code", "text"])

    def test_razor_comment_counts_its_opening_line(self):
        kinds = cb.classify(["@* Why one,", "   why two. *@", "<div></div>"], cb.RAZOR)
        self.assertEqual(kinds, ["text", "text", "code"])

    def test_razor_comment_opened_after_markup(self):
        kinds = cb.classify(["<p>Don't @* why", "   more *@"], cb.RAZOR)
        self.assertEqual(kinds, ["text", "text"])

    def test_block_opened_after_code_counts_its_opening_line(self):
        self.assertEqual(cb.classify(["x(); /* a", " b", " c", " d */"]), ["text"] * 4)
        self.assertEqual(cb.classify(["x(); /*", " b", " */"]), ["code", "text", "tag"])

    def test_html_comment_in_razor(self):
        self.assertEqual(cb.classify(["<!--", "why", "-->"], cb.RAZOR), ["tag", "text", "tag"])

    def test_template_literal_in_javascript(self):
        self.assertEqual(cb.classify(["const g = `/*`;", "// a"], cb.JAVASCRIPT), ["code", "text"])


class ParseAddedTests(unittest.TestCase):
    def test_edge_cases(self):
        cases = [
            ("path with a space", "+++ b/src/A B.cs\t\n@@ -0,0 +1,2 @@\n", {"src/A B.cs": {1, 2}}),
            ("deleted file", "--- a/src/A.cs\n+++ /dev/null\n@@ -1,3 +0,0 @@\n", {}),
            ("hunk that only removes", "+++ b/src/A.cs\n@@ -4,2 +3,0 @@\n", {"src/A.cs": set()}),
            ("count omitted means one", "+++ b/src/A.cs\n@@ -4 +7 @@\n", {"src/A.cs": {7}}),
        ]
        for name, diff, expected in cases:
            with self.subTest(name):
                self.assertEqual(cb.parse_added(diff), expected)


class ModeTests(unittest.TestCase):
    """The check end to end, in a throwaway git repository."""

    def setUp(self):
        self.cwd = os.getcwd()
        # A forge worktree exports these, and they win over the cwd: git would judge the real branch.
        self.saved = {k: os.environ.pop(k) for k in ("GIT_DIR", "GIT_WORK_TREE", "GIT_INDEX_FILE", "GIT_COMMON_DIR")
                      if k in os.environ}
        self.tmp = tempfile.TemporaryDirectory(ignore_cleanup_errors=True)
        os.chdir(self.tmp.name)
        self.git("init", "-q")
        self.write("src/Fhi.Munin.Explorer/App.cs", "class A {}\n", track=True)
        self.git("commit", "-qm", "base")
        self.base = self.git("rev-parse", "HEAD").strip()

    def tearDown(self):
        os.chdir(self.cwd)
        os.environ.update(self.saved)
        self.tmp.cleanup()

    def git(self, *args):
        return subprocess.run(
            ["git", "-c", "user.name=t", "-c", "user.email=t@t", "-c", "core.autocrlf=false", *args],
            capture_output=True, text=True, check=True,
        ).stdout

    def write(self, path, content, track=False):
        os.makedirs(os.path.dirname(path) or ".", exist_ok=True)
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write(content)
        if track:
            self.git("add", path)

    def run_check(self):
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            code = cb.check(self.base)
        return code, out.getvalue()

    def assert_fails(self, path, content):
        self.write(path, content, track=True)
        code, out = self.run_check()
        self.assertEqual(code, 1, out)
        self.assertIn(f"::error file={path}", out)

    def assert_passes_clean(self, path, content):
        self.write(path, content, track=True)
        code, out = self.run_check()
        self.assertEqual(code, 0, out)
        self.assertIn("0 error(s), 0 warning(s)", out)

    def test_production_essay_fails(self):
        self.assert_fails("src/Fhi.Munin.Explorer/B.cs", ESSAY + "class B {}\n")

    def test_three_lines_pass(self):
        self.assert_passes_clean("src/Fhi.Munin.Explorer/B.cs", "// a\n// b\n// c\nclass B {}\n")

    def test_test_project_only_warns(self):
        self.write("test/Fhi.Munin.Explorer.Tests/BTest.cs", ESSAY + "class BTest {}\n", track=True)
        code, out = self.run_check()
        self.assertEqual(code, 0, out)
        self.assertIn("::warning file=test/", out)

    def test_paths_outside_src_and_test_are_not_read(self):
        self.assert_passes_clean("samples/ModernHost/Program.cs", ESSAY + "class P {}\n")

    def test_razor_essay_fails(self):
        self.assert_fails("src/Fhi.Munin.Explorer/Blazor/B.razor", "@* a\n   b\n   c\n   d *@\n<div></div>\n")

    def test_javascript_essay_fails(self):
        self.assert_fails("src/Fhi.Munin.Explorer/wwwroot/x.js", ESSAY + "export const x = 1;\n")

    def test_public_member_doc_of_public_type_is_exempt(self):
        self.assert_passes_clean(
            "src/Fhi.Munin.Explorer/B.cs",
            "namespace N;\n\npublic sealed class B\n{\n" + DOC + "    [Parameter]\n    public string? X { get; set; }\n}\n")

    def test_public_type_doc_is_exempt(self):
        self.assert_passes_clean("src/Fhi.Munin.Explorer/B.cs", DOC.replace("    ", "") + "public sealed record B(int X);\n")

    def test_interface_member_without_modifier_is_exempt(self):
        self.assert_passes_clean(
            "src/Fhi.Munin.Explorer/I.cs", "public interface I\n{\n" + DOC + "    Task<int> GetAsync();\n}\n")

    def test_protected_member_doc_is_exempt(self):
        for modifier in ("protected", "protected internal"):
            with self.subTest(modifier):
                self.assert_passes_clean(
                    "src/Fhi.Munin.Explorer/B.cs",
                    "public class B\n{\n" + DOC + f"    {modifier} virtual void M() {{ }}\n}}\n")

    def test_private_protected_member_doc_fails(self):
        self.assert_fails(
            "src/Fhi.Munin.Explorer/B.cs", "public class B\n{\n" + DOC + "    private protected void M() { }\n}\n")

    def test_class_member_without_modifier_fails(self):
        self.assert_fails("src/Fhi.Munin.Explorer/B.cs", "public class B\n{\n" + DOC + "    void M() { }\n}\n")

    def test_enum_member_is_exempt(self):
        self.assert_passes_clean("src/Fhi.Munin.Explorer/E.cs", "public enum E\n{\n" + DOC + "    One,\n}\n")

    def test_block_scoped_namespace_is_walked_through(self):
        self.assert_passes_clean(
            "src/Fhi.Munin.Explorer/B.cs",
            "namespace N\n{\n    public sealed class B\n    {\n" + DOC.replace("    ", "        ")
            + "        public int X { get; }\n    }\n}\n")
        self.assert_fails(
            "src/Fhi.Munin.Explorer/C.cs",
            "namespace N\n{\n    internal sealed class C\n    {\n" + DOC.replace("    ", "        ")
            + "        public int X { get; }\n    }\n}\n")

    def test_public_member_doc_of_internal_type_fails(self):
        self.assert_fails(
            "src/Fhi.Munin.Explorer/B.cs", "internal sealed class B\n{\n" + DOC + "    public string? X { get; set; }\n}\n")

    def test_private_member_doc_fails(self):
        self.assert_fails(
            "src/Fhi.Munin.Explorer/B.cs", "public sealed class B\n{\n" + DOC + "    private void M() { }\n}\n")

    def test_nested_public_type_in_internal_type_fails(self):
        self.assert_fails(
            "src/Fhi.Munin.Explorer/B.cs",
            "internal static class B\n{\n    public sealed class C\n    {\n" + DOC.replace("    ", "        ")
            + "        public int X { get; }\n    }\n}\n")

    def test_line_comment_before_public_member_is_not_a_doc(self):
        self.assert_fails(
            "src/Fhi.Munin.Explorer/B.cs", "public sealed class B\n{\n" + ESSAY + "    public int X { get; }\n}\n")

    def test_public_doc_outside_the_package_fails(self):
        self.assert_fails("src/Other/B.cs", DOC.replace("    ", "") + "public sealed record B(int X);\n")

    def test_razor_code_block_public_parameter_is_exempt(self):
        self.assert_passes_clean(
            "src/Fhi.Munin.Explorer/Blazor/B.razor",
            "<div></div>\n\n@code {\n" + DOC + "    [Parameter] public string? X { get; set; }\n}\n")

    def test_razor_code_block_private_member_fails(self):
        self.assert_fails(
            "src/Fhi.Munin.Explorer/Blazor/B.razor", "<div></div>\n\n@code {\n" + DOC + "    private int _x;\n}\n")

    def test_untouched_long_block_is_not_reported(self):
        self.write("src/Fhi.Munin.Explorer/Old.cs", ESSAY + "class Old {}\n", track=True)
        self.git("commit", "-qm", "old")
        self.base = self.git("rev-parse", "HEAD").strip()
        self.assert_passes_clean("src/Fhi.Munin.Explorer/Old.cs", ESSAY + "class Old { int x; }\n")

    def test_one_line_added_to_a_long_block_only_warns(self):
        self.write("src/Fhi.Munin.Explorer/Old.cs", ESSAY + "class Old {}\n", track=True)
        self.git("commit", "-qm", "old")
        self.base = self.git("rev-parse", "HEAD").strip()
        self.write("src/Fhi.Munin.Explorer/Old.cs", ESSAY + "// e\nclass Old {}\n", track=True)
        code, out = self.run_check()
        self.assertEqual(code, 0, out)
        self.assertIn("touches a 5-line comment block", out)

    def test_generated_file_is_skipped(self):
        self.assert_passes_clean("src/Fhi.Munin.Explorer/G.cs", "// <auto-generated/>\n\n" + ESSAY + "class G {}\n")

    def test_allowlisted_block_passes(self):
        self.write("src/Fhi.Munin.Explorer/B.cs", ESSAY + "class B {}\n", track=True)
        self.write(cb.ALLOWLIST, "src/Fhi.Munin.Explorer/B.cs | // b | the race cannot be read from the code\n")
        code, out = self.run_check()
        self.assertEqual(code, 0, out)


if __name__ == "__main__":
    unittest.main()
