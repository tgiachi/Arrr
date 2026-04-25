import { IconButton } from '@chakra-ui/react'
import { Moon, Sun } from 'lucide-react'
import { useTheme } from 'next-themes'

export function ThemeToggle() {
  const { resolvedTheme, setTheme } = useTheme()
  const isDark = resolvedTheme !== 'light'

  return (
    <IconButton
      aria-label={isDark ? 'Switch to light mode' : 'Switch to dark mode'}
      size="sm"
      variant="ghost"
      color="app.textMuted"
      _hover={{ color: 'amber.300', bg: 'whiteAlpha.50' }}
      onClick={() => setTheme(isDark ? 'light' : 'dark')}
      style={{ transition: 'color 0.2s' }}
    >
      <span
        style={{
          display: 'flex',
          alignItems: 'center',
          transition: 'transform 0.35s cubic-bezier(0.34,1.56,0.64,1), opacity 0.2s',
          transform: isDark ? 'rotate(0deg) scale(1)' : 'rotate(20deg) scale(0.9)',
        }}
      >
        {isDark ? <Sun size={15} /> : <Moon size={15} />}
      </span>
    </IconButton>
  )
}
