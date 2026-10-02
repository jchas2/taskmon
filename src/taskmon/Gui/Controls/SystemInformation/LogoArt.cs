namespace Task.Monitor.Gui.Controls.SystemInformation;

// Single source of truth for the ascii-art glyph text shared by AboutScreen's rotating logo and
// SystemLogoControl's pulsing logo. Colour application is entirely up to each consumer.
internal static class LogoArt
{
#if __APPLE__
    public static readonly string[] Lines =
    {
        "                  .,o",
        "                 /gg,",
        "              (dMMb",
        "               .o,",
        "    .gggMbgg.     .,ggMMg,",
        "   dMMMMMMMMMMMMMMMMMMMMMMb",
        "  dMMMMMMMMMMMMMMMMMMMMMMMMb",
        " dMMMMMMMMMMMMMMMMMMMMMMMMMMb",
        ".MMMMMMMMMMMMMMMMMMMMMMMMMb,",
        "MMMMMMMMMMMMMMMMMMMMMMMMM`",
        "MMMMMMMMMMMMMMMMMMMMMMMM`",
        "MMMMMMMMMMMMMMMMMMMMMMMM,",
        "MMMMMMMMMMMMMMMMMMMMMMMMM.",
        ".MMMMMMMMMMMMMMMMMMMMMMMMMM'",
        " `MMMMMMMMMMMMMMMMMMMMMMMMMMd'",
        "  `bMMMMMMMMMMMMMMMMMMMMMMMMd'",
        "   `bMMMMMMMMMTASKMMMMMMMMMd'",
        "     `MbMMMMMMONITORMMMMMdM'",
        "       `MMbgg,,,,,,,ggdMM'",
        "         `''        ''`",
    };
#endif
#if __WIN32__
    public static readonly string[] Lines =
    {
        "        ,.=:!!t3Z3z.,",
        "       :tt:::tt333EE3",
        "       Et:::ztt33EEEL @Ee.,      ..,",
        "      ;tt:::tt333EE7 ;EEEEEEttttt33#",
        "     :Et:::zt333EEQ. $EEEEEttttt33QL",
        "     it::::tt333EEF @EEEEEEttttt33F",
        "    ;3=*^```'*4EEV :EEEEEEttttt33@.",
        "    ,.=::::it=., ` @EEEEEEtttz33QF",
        "   ;::::::::zt33)   \"4EEEtttji3P*",
        "  :t::::::::tt33.:Z3z..  `` ,..g.",
        "  i::::::::zt33F ATASKttt::::ztF",
        " ;:::::::::t33V ;MONITORt::::t3",
        " E::::::::zt33L @EEEtttt::::z3F",
        "{3=*^```'*4E3) ;EEEtttt:::::tZ`",
        "             ` :EEEEtttt::::z7",
        "                 \"VEzjt:;;z>*`",
    };
#endif
}
