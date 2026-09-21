// Dumps the Rust primitives the port would use, in the format of specs/004-python-port/tools/dump.py.
use std::fmt::Write as _;
use std::io::Write as _;
use unicode_normalization::UnicodeNormalization;

fn dotnet_ws(u: u32) -> bool {
    matches!(u, 0x09..=0x0D | 0x20 | 0x85 | 0xA0 | 0x1680 | 0x2000..=0x200A | 0x2028 | 0x2029 | 0x202F | 0x205F | 0x3000)
}

fn hexs(s: &str) -> String {
    let mut out = String::new();
    for (i, c) in s.chars().enumerate() {
        if i > 0 { out.push(' '); }
        write!(out, "{:04X}", c as u32).unwrap();
    }
    out
}

fn main() {
    let dir = std::env::args().nth(1).expect("dir");
    let mut units = std::io::BufWriter::new(std::fs::File::create(format!("{dir}/rust-units.txt")).unwrap());
    let mut ws_std_diff = Vec::new();
    for u in 0u32..=0xFFFF {
        let lower = match char::from_u32(u) {
            Some(c) => {
                if c.is_whitespace() != dotnet_ws(u) { ws_std_diff.push(format!("{u:04X}")); }
                let mut l = c.to_lowercase();
                match (l.next(), l.next()) {
                    (Some(one), None) if (one as u32) <= 0xFFFF => one as u32,
                    _ => u,
                }
            }
            None => u,
        };
        writeln!(units, "{u:04X} XX {} {lower:04X}", if dotnet_ws(u) { 1 } else { 0 }).unwrap();
    }
    let mut points = std::io::BufWriter::new(std::fs::File::create(format!("{dir}/rust-points.txt")).unwrap());
    for cp in 0u32..=0x10FFFF {
        let Some(c) = char::from_u32(cp) else { continue };
        let s = c.to_string();
        writeln!(points, "{cp:04X} XX {} | {}", hexs(&s.nfkc().collect::<String>()), hexs(&s.nfd().collect::<String>())).unwrap();
    }
    println!("rust std unicode {:?}, unicode-normalization {:?}, char::is_whitespace differs from .NET on {:?}",
        char::UNICODE_VERSION, unicode_normalization::UNICODE_VERSION, ws_std_diff);
}
