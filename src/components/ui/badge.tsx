import * as React from "react";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "@/lib/utils";

const badgeVariants = cva(
  "inline-flex items-center rounded-full border px-3 py-1 text-xs font-semibold transition-colors",
  {
    variants: {
      variant: {
        default:
          "border-white/15 bg-white/10 text-white",
        secondary:
          "border-white/10 bg-white/5 text-white/80",
        success:
          "border-emerald-400/20 bg-emerald-500/15 text-emerald-200",
        warning:
          "border-amber-400/20 bg-amber-500/15 text-amber-200",
        danger:
          "border-red-400/20 bg-red-500/15 text-red-200",
        info:
          "border-blue-400/20 bg-blue-500/15 text-blue-200",
      },
    },
    defaultVariants: {
      variant: "default",
    },
  }
);

export interface BadgeProps
  extends React.HTMLAttributes<HTMLDivElement>,
    VariantProps<typeof badgeVariants> {}

function Badge({ className, variant, ...props }: BadgeProps) {
  return (
    <div className={cn(badgeVariants({ variant }), className)} {...props} />
  );
}

export { Badge, badgeVariants };
